using System;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework;
using TrafficOptimizer.Core;
using TrafficOptimizer.Patches;
using UnityEngine;

namespace TrafficOptimizer.Services
{
    public sealed class JunctionConnectionService : ServiceBase
    {
        private const int DebounceFrames = 240;
        private const int MaximumConnectionsPerNode = 2048;
        private static JunctionConnectionService _instance;

        private NodeSignature[] _knownNodes;
        private int _observedRevision;
        private ulong _processAfterFrame;
        private bool _scanPending;

        public override string Name { get { return "TM:PE Junction Connections"; } }

        public static void OnCityLoaded()
        {
            if (_instance != null)
                _instance.CaptureBaseline();
        }

        public static void OnCityUnloading()
        {
            if (_instance != null)
            {
                _instance._knownNodes = null;
                _instance._scanPending = false;
            }
        }

        protected override void OnInitialize()
        {
            _instance = this;
            if (GameState.CityLoaded)
                CaptureBaseline();
        }

        protected override void OnUpdate()
        {
            if (!GameState.CityLoaded)
                return;

            int revision = NetworkChangePatch.Revision;
            if (revision != _observedRevision)
            {
                _observedRevision = revision;
                _scanPending = true;
                _processAfterFrame = GameState.SimulationFrame + DebounceFrames;
            }

            if (!_scanPending || GameState.SimulationFrame < _processAfterFrame)
                return;

            _scanPending = false;
            ScanChangedNodes();
        }

        protected override void OnShutdown()
        {
            _knownNodes = null;
            _scanPending = false;
            if (object.ReferenceEquals(_instance, this))
                _instance = null;
        }

        private void CaptureBaseline()
        {
            NetManager manager = Singleton<NetManager>.instance;
            if (manager == null)
                return;

            _knownNodes = new NodeSignature[manager.m_nodes.m_buffer.Length];
            for (int i = 0; i < _knownNodes.Length; i++)
                _knownNodes[i] = ReadSignature(manager.m_nodes.m_buffer[i]);

            _observedRevision = NetworkChangePatch.Revision;
            _scanPending = false;
        }

        private void ScanChangedNodes()
        {
            NetManager manager = Singleton<NetManager>.instance;
            if (manager == null)
                return;

            if (_knownNodes == null || _knownNodes.Length != manager.m_nodes.m_buffer.Length)
            {
                CaptureBaseline();
                return;
            }

            for (int i = 0; i < _knownNodes.Length; i++)
            {
                NetNode node = manager.m_nodes.m_buffer[i];
                NodeSignature current = ReadSignature(node);
                if (_knownNodes[i].Equals(current))
                    continue;

                _knownNodes[i] = current;
                if (current.Created && Config.Data.EnableAutomaticLaneConnections)
                    TryConfigureNode((ushort)i, node);
            }
        }

        private static NodeSignature ReadSignature(NetNode node)
        {
            if ((node.m_flags & NetNode.Flags.Created) == 0)
                return new NodeSignature();

            return new NodeSignature(
                node.m_segment0, node.m_segment1, node.m_segment2, node.m_segment3,
                node.m_segment4, node.m_segment5, node.m_segment6, node.m_segment7);
        }

        private static void TryConfigureNode(ushort nodeId, NetNode node)
        {
            object manager;
            MethodInfo hasConnections;
            MethodInfo addConnection;
            MethodInfo removeConnection;
            object transitionGroup;
            if (!TMPEBridge.TryGetLaneConnectionApi(
                    out manager, out hasConnections, out addConnection,
                    out removeConnection, out transitionGroup))
                return;

            try
            {
                if ((bool)hasConnections.Invoke(manager, new object[] { nodeId }))
                    return;

                List<Approach> approaches = BuildApproaches(nodeId, node);
                if (approaches.Count < 3)
                    return;

                JunctionTemplate template = DetectTemplate(approaches);
                List<Connection> connections = BuildConnections(approaches, template);
                if (!HasCompleteCoverage(approaches, connections) ||
                    connections.Count == 0 || connections.Count > MaximumConnectionsPerNode)
                    return;

                List<Connection> added = new List<Connection>(connections.Count);
                for (int i = 0; i < connections.Count; i++)
                {
                    Connection connection = connections[i];
                    object result = addConnection.Invoke(manager, new object[]
                    {
                        connection.FromLane,
                        connection.ToLane,
                        connection.AtStartNode,
                        transitionGroup
                    });

                    if (result is bool && !(bool)result)
                    {
                        Rollback(manager, removeConnection, transitionGroup, added);
                        return;
                    }

                    added.Add(connection);
                }

                Core.Logger.Info("Applied " + template + " lane template with " + connections.Count +
                                 " TM:PE connections at node " + nodeId + ".");
            }
            catch (Exception ex)
            {
                Core.Logger.Warning("TM:PE lane connections skipped at node " + nodeId + ": " + ex.Message);
            }
        }

        private static List<Approach> BuildApproaches(ushort nodeId, NetNode node)
        {
            List<Approach> approaches = new List<Approach>(8);
            NetManager manager = Singleton<NetManager>.instance;
            if (manager == null)
                return approaches;

            for (int i = 0; i < 8; i++)
            {
                ushort segmentId = GetSegmentId(node, i);
                if (segmentId == 0 || segmentId >= manager.m_segments.m_buffer.Length)
                    continue;

                NetSegment segment = manager.m_segments.m_buffer[segmentId];
                if ((segment.m_flags & NetSegment.Flags.Created) == 0 || segment.Info == null)
                    continue;

                bool isStartNode;
                if (segment.m_startNode == nodeId)
                    isStartNode = true;
                else if (segment.m_endNode == nodeId)
                    isStartNode = false;
                else
                    continue;

                Vector3 outward = isStartNode ? segment.m_startDirection : -segment.m_endDirection;
                outward.y = 0f;
                if (outward.sqrMagnitude < 0.001f)
                    continue;

                Approach approach = new Approach(segmentId, isStartNode, outward.normalized);
                if (!ReadVehicleLanes(manager, segmentId, segment, node.m_position, approach))
                    continue;

                approach.Incoming.Sort(CompareLanePositionDescending);
                approach.Outgoing.Sort(CompareLanePositionDescending);

                approaches.Add(approach);
            }

            return approaches;
        }

        private static bool ReadVehicleLanes(
            NetManager manager,
            ushort segmentId,
            NetSegment segment,
            Vector3 nodePosition,
            Approach approach)
        {
            NetInfo.Lane[] laneInfos = segment.Info.m_lanes;
            if (laneInfos == null || laneInfos.Length == 0)
                return false;

            bool invert = (segment.m_flags & NetSegment.Flags.Invert) != 0;
            uint laneId = segment.m_lanes;
            int laneIndex = 0;
            while (laneId != 0 && laneIndex < laneInfos.Length)
            {
                if (laneId >= manager.m_lanes.m_buffer.Length)
                    return false;

                NetLane lane = manager.m_lanes.m_buffer[laneId];
                NetInfo.Lane laneInfo = laneInfos[laneIndex];
                if (lane.m_segment == segmentId &&
                    (laneInfo.m_laneType & NetInfo.LaneType.Vehicle) != 0 &&
                    (laneInfo.m_vehicleType & VehicleInfo.VehicleType.Car) != 0)
                {
                    bool forward = (laneInfo.m_direction & NetInfo.Direction.Forward) != 0;
                    bool backward = (laneInfo.m_direction & NetInfo.Direction.Backward) != 0;
                    if (invert)
                    {
                        bool swap = forward;
                        forward = backward;
                        backward = swap;
                    }

                    bool inbound = approach.AtStartNode ? backward : forward;
                    bool outbound = approach.AtStartNode ? forward : backward;
                    Vector3 laneEndpoint = approach.AtStartNode ? lane.m_bezier.a : lane.m_bezier.d;
                    if (inbound)
                        approach.Incoming.Add(new LaneEnd(
                            laneId,
                            approach.AtStartNode,
                            lane.m_flags,
                            GetLateralPosition(laneEndpoint, nodePosition, -approach.OutwardDirection)));
                    if (outbound)
                        approach.Outgoing.Add(new LaneEnd(
                            laneId,
                            approach.AtStartNode,
                            lane.m_flags,
                            GetLateralPosition(laneEndpoint, nodePosition, approach.OutwardDirection)));
                }

                laneId = lane.m_nextLane;
                laneIndex++;
            }

                 return laneIndex == laneInfos.Length &&
                     (approach.Incoming.Count > 0 || approach.Outgoing.Count > 0);
        }

        private static List<Connection> BuildConnections(
            List<Approach> approaches,
            JunctionTemplate template)
        {
            List<Connection> connections = new List<Connection>();
            for (int i = 0; i < approaches.Count; i++)
            {
                Approach from = approaches[i];
                Vector3 inboundDirection = -from.OutwardDirection;
                for (int laneIndex = 0; laneIndex < from.Incoming.Count; laneIndex++)
                {
                    LaneEnd incoming = from.Incoming[laneIndex];
                    int laneTurns = GetLaneTurnMask(incoming.Flags);

                    for (int j = 0; j < approaches.Count; j++)
                    {
                        if (i == j)
                            continue;

                        Approach to = approaches[j];
                        int movement = GetMovementMask(inboundDirection, to.OutwardDirection, template);
                        if (movement == 0 || (laneTurns != 0 && (laneTurns & movement) == 0))
                            continue;

                        List<LaneEnd> targets = SelectTemplateTargets(
                            from, incoming, to, movement);
                        for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                        {
                            connections.Add(new Connection(
                                incoming.LaneId,
                                targets[targetIndex].LaneId,
                                incoming.AtStartNode));
                        }
                    }
                }
            }

            return connections;
        }

        private static int GetLaneTurnMask(ushort laneFlags)
        {
            int mask = 0;
            if ((laneFlags & (ushort)NetLane.Flags.Left) != 0)
                mask |= 1;
            if ((laneFlags & (ushort)NetLane.Flags.Forward) != 0)
                mask |= 2;
            if ((laneFlags & (ushort)NetLane.Flags.Right) != 0)
                mask |= 4;
            return mask;
        }

        private static int GetMovementMask(
            Vector3 inbound,
            Vector3 outbound,
            JunctionTemplate template)
        {
            float dot = inbound.x * outbound.x + inbound.z * outbound.z;
            float straightThreshold = template == JunctionTemplate.Y ? 0.82f : 0.65f;
            if (dot > straightThreshold)
                return 2;
            if (dot < -0.95f)
                return 0;

            float cross = inbound.z * outbound.x - inbound.x * outbound.z;
            return cross < 0f ? 1 : 4;
        }

        private static List<LaneEnd> SelectTemplateTargets(
            Approach source,
            LaneEnd incoming,
            Approach target,
            int movement)
        {
            List<LaneEnd> selected = new List<LaneEnd>();
            if (target.Outgoing.Count == 0)
                return selected;

            if (movement == 1)
            {
                selected.Add(target.Outgoing[0]);
                return selected;
            }

            if (movement == 4)
            {
                selected.Add(target.Outgoing[target.Outgoing.Count - 1]);
                return selected;
            }

            int sourceRank = source.Incoming.IndexOf(incoming);
            int sourceCount = source.Incoming.Count;
            int targetCount = target.Outgoing.Count;
            if (sourceRank < 0 || sourceCount == 0)
                return selected;

            int firstTarget = sourceRank * targetCount / sourceCount;
            int lastTarget = ((sourceRank + 1) * targetCount - 1) / sourceCount;
            if (lastTarget >= targetCount)
                lastTarget = targetCount - 1;

            for (int i = firstTarget; i <= lastTarget; i++)
                selected.Add(target.Outgoing[i]);

            return selected;
        }

        private static float GetLateralPosition(
            Vector3 laneEndpoint,
            Vector3 nodePosition,
            Vector3 travelDirection)
        {
            Vector3 left = new Vector3(-travelDirection.z, 0f, travelDirection.x);
            Vector3 offset = laneEndpoint - nodePosition;
            return offset.x * left.x + offset.z * left.z;
        }

        private static int CompareLanePositionDescending(LaneEnd left, LaneEnd right)
        {
            return right.LateralPosition.CompareTo(left.LateralPosition);
        }

        private static bool HasCompleteCoverage(List<Approach> approaches, List<Connection> connections)
        {
            HashSet<uint> incoming = new HashSet<uint>();
            HashSet<uint> outgoing = new HashSet<uint>();
            HashSet<uint> reached = new HashSet<uint>();
            HashSet<uint> served = new HashSet<uint>();

            for (int i = 0; i < approaches.Count; i++)
            {
                for (int j = 0; j < approaches[i].Incoming.Count; j++)
                    incoming.Add(approaches[i].Incoming[j].LaneId);
                for (int j = 0; j < approaches[i].Outgoing.Count; j++)
                    outgoing.Add(approaches[i].Outgoing[j].LaneId);
            }

            for (int i = 0; i < connections.Count; i++)
            {
                served.Add(connections[i].FromLane);
                reached.Add(connections[i].ToLane);
            }

            if (incoming.Count == 0 || outgoing.Count == 0 || incoming.Count != served.Count)
                return false;

            foreach (uint laneId in incoming)
                if (!served.Contains(laneId)) return false;
            foreach (uint laneId in outgoing)
                if (!reached.Contains(laneId)) return false;

            return true;
        }

        private static JunctionTemplate DetectTemplate(List<Approach> approaches)
        {
            if (approaches.Count == 3)
            {
                for (int i = 0; i < approaches.Count; i++)
                {
                    for (int j = i + 1; j < approaches.Count; j++)
                    {
                        if (Vector3.Dot(approaches[i].OutwardDirection, approaches[j].OutwardDirection) < -0.75f)
                        {
                            int thirdIndex = 3 - i - j;
                            Vector3 axis = (approaches[i].OutwardDirection - approaches[j].OutwardDirection).normalized;
                            float sideAngle = Math.Abs(Vector3.Dot(axis, approaches[thirdIndex].OutwardDirection));
                            return sideAngle < 0.35f ? JunctionTemplate.T : JunctionTemplate.Y;
                        }
                    }
                }
                return JunctionTemplate.Y;
            }

            if (approaches.Count == 4)
            {
                int oppositePairs = 0;
                for (int i = 0; i < approaches.Count; i++)
                    for (int j = i + 1; j < approaches.Count; j++)
                        if (Vector3.Dot(approaches[i].OutwardDirection, approaches[j].OutwardDirection) < -0.75f)
                            oppositePairs++;
                return oppositePairs >= 2 ? JunctionTemplate.X : JunctionTemplate.FourWay;
            }
            return JunctionTemplate.MultiWay;
        }

        private static ushort GetSegmentId(NetNode node, int index)
        {
            switch (index)
            {
                case 0: return node.m_segment0;
                case 1: return node.m_segment1;
                case 2: return node.m_segment2;
                case 3: return node.m_segment3;
                case 4: return node.m_segment4;
                case 5: return node.m_segment5;
                case 6: return node.m_segment6;
                default: return node.m_segment7;
            }
        }

        private static void Rollback(
            object manager,
            MethodInfo removeMethod,
            object transitionGroup,
            List<Connection> added)
        {
            for (int i = added.Count - 1; i >= 0; i--)
            {
                Connection connection = added[i];
                try
                {
                    removeMethod.Invoke(manager, new object[]
                    {
                        connection.FromLane,
                        connection.ToLane,
                        connection.AtStartNode,
                        transitionGroup
                    });
                }
                catch
                {
                }
            }
        }

        private sealed class Approach
        {
            public readonly ushort SegmentId;
            public readonly bool AtStartNode;
            public readonly Vector3 OutwardDirection;
            public readonly List<LaneEnd> Incoming = new List<LaneEnd>();
            public readonly List<LaneEnd> Outgoing = new List<LaneEnd>();

            public Approach(ushort segmentId, bool atStartNode, Vector3 outwardDirection)
            {
                SegmentId = segmentId;
                AtStartNode = atStartNode;
                OutwardDirection = outwardDirection;
            }
        }

        private sealed class LaneEnd
        {
            public readonly uint LaneId;
            public readonly bool AtStartNode;
            public readonly ushort Flags;
            public readonly float LateralPosition;

            public LaneEnd(uint laneId, bool atStartNode, ushort flags, float lateralPosition)
            {
                LaneId = laneId;
                AtStartNode = atStartNode;
                Flags = flags;
                LateralPosition = lateralPosition;
            }
        }

        private enum JunctionTemplate
        {
            T,
            Y,
            X,
            FourWay,
            MultiWay
        }

        private struct Connection
        {
            public readonly uint FromLane;
            public readonly uint ToLane;
            public readonly bool AtStartNode;

            public Connection(uint fromLane, uint toLane, bool atStartNode)
            {
                FromLane = fromLane;
                ToLane = toLane;
                AtStartNode = atStartNode;
            }
        }

        private struct NodeSignature : IEquatable<NodeSignature>
        {
            public readonly bool Created;
            private readonly ushort _segment0;
            private readonly ushort _segment1;
            private readonly ushort _segment2;
            private readonly ushort _segment3;
            private readonly ushort _segment4;
            private readonly ushort _segment5;
            private readonly ushort _segment6;
            private readonly ushort _segment7;

            public NodeSignature(
                ushort segment0, ushort segment1, ushort segment2, ushort segment3,
                ushort segment4, ushort segment5, ushort segment6, ushort segment7)
            {
                Created = true;
                _segment0 = segment0;
                _segment1 = segment1;
                _segment2 = segment2;
                _segment3 = segment3;
                _segment4 = segment4;
                _segment5 = segment5;
                _segment6 = segment6;
                _segment7 = segment7;
            }

            public bool Equals(NodeSignature other)
            {
                return Created == other.Created && _segment0 == other._segment0 &&
                       _segment1 == other._segment1 && _segment2 == other._segment2 &&
                       _segment3 == other._segment3 && _segment4 == other._segment4 &&
                       _segment5 == other._segment5 && _segment6 == other._segment6 &&
                       _segment7 == other._segment7;
            }
        }
    }
}