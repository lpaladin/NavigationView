using Game.Citizens;
using Game.Common;
using Game.Creatures;
using Game.Net;
using Game.Pathfind;
using Game.Routes;
using Game.Vehicles;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Game.UI;
using System.Reflection;
using UnityEngine;
using Unity.Burst;
using Colossal.Mathematics;
using Unity.Mathematics;
using Game.Buildings;

namespace NavigationView
{
    [BurstCompile]
    public struct FetchNavigationEntriesJob : IJob
    {
        [ReadOnly]
        public Entity citizen;
        [ReadOnly]
        public EntityManager manager;
        [ReadOnly]
        public ComponentLookup<PathOwner> pathOwnerLookup;
        [ReadOnly]
        public ComponentLookup<Owner> ownerLookup;
        [ReadOnly]
        public ComponentLookup<RouteLane> routeLaneLookup;
        [ReadOnly]
        public ComponentLookup<Waypoint> waypointLookup;
        [ReadOnly]
        public ComponentLookup<Aggregated> aggregatedLookup;
        [ReadOnly]
        public ComponentLookup<PedestrianLane> pedestrianLaneLookup;
        [ReadOnly]
        public ComponentLookup<HumanCurrentLane> humanLaneLookup;
        [ReadOnly]
        public ComponentLookup<Curve> curveLookup;
        [ReadOnly]
        public ComponentLookup<CarCurrentLane> carLaneLookup;
        [ReadOnly]
        public ComponentLookup<TrackLane> trackLaneLookup;
        [ReadOnly]
        public ComponentLookup<Game.Routes.Color> colorLookup;
        [ReadOnly]
        public ComponentLookup<SecondaryLane> secondaryLaneLookup;
        [ReadOnly]
        public ComponentLookup<CurrentVehicle> currentVehicleLookup;
        [ReadOnly]
        public ComponentLookup<CurrentTransport> currentTransportLookup;
        [ReadOnly]
        public ComponentLookup<Connected> connectedLookup;
        [ReadOnly]
        public ComponentLookup<Game.Common.Target> targetLookup;
        [ReadOnly]
        public ComponentLookup<WaitingPassengers> waitingPassengersLookup;
        [ReadOnly]
        public ComponentLookup<Deleted> deletedLookup;
        [ReadOnly]
        public ComponentLookup<Game.Prefabs.PrefabRef> prefabRefLookup;
        [ReadOnly]
        public ComponentLookup<Game.Prefabs.TransportLineData> transportLineDataLookup;
        [ReadOnly]
        public ComponentLookup<TrainCurrentLane> trainCurrentLaneLookup;
        [ReadOnly]
        public ComponentLookup<WatercraftCurrentLane> watercraftCurrentLaneLookup;
        [ReadOnly]
        public ComponentLookup<AircraftCurrentLane> aircraftCurrentLaneLookup;
        [ReadOnly]
        public ComponentLookup<Building> buildingLookup;
        [ReadOnly]
        public ComponentLookup<Game.Objects.Transform> transformLookup;
        [ReadOnly]
        public ComponentLookup<Controller> controllerLookup;
        [ReadOnly]
        public EntityStorageInfoLookup storageInfoLookup;
        [ReadOnly]
        public BufferLookup<PathElement> pathElementLookup;
        [ReadOnly]
        public BufferLookup<RouteSegment> routeSegmentLookup;
        [ReadOnly]
        public BufferLookup<CarNavigationLane> carNavigationLaneSegmentLookup;
        [ReadOnly]
        public BufferLookup<RouteWaypoint> routeWaypointLookup;
        [ReadOnly]
        public BufferLookup<TrainNavigationLane> trainNavigationLaneLookup;
        [ReadOnly]
        public BufferLookup<WatercraftNavigationLane> watercraftNavigationLaneLookup;
        [ReadOnly]
        public BufferLookup<AircraftNavigationLane> aircraftNavigationLaneLookup;

        public NativeList<NativeNavigationEntry> resultEntries;
        public NativeList<NativeNavigationEntryPath> resultEntryPaths;

        private Entity GetAncestor(Entity entity)
        {
            while (ownerLookup.TryGetComponent(entity, out var owner))
            {
                entity = owner.m_Owner;
            }
            return entity;
        }

        private string GetName(in Entity e)
        {
            var name = NavigationRouteListSystem.nameSystem.GetName(e);
            var a = typeof(NameSystem.Name).GetField("m_NameType", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(name);
            var b = typeof(NameSystem.Name).GetField("m_NameID", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(name);
            var c = (string[]) typeof(NameSystem.Name).GetField("m_NameArgs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(name);
            return $"{a} {b} {(c == null ? "" : string.Join(",", c))}";
        }

        private string ToDebugString(in Entity e, string indent = "")
        {
            var types = manager.GetComponentTypes(e);
            var sb = new StringBuilder(indent);
            foreach (var type in types)
            {
                var mtype = type.GetManagedType();
                sb.Append(mtype);
                sb.Append(", ");
            }
            sb.AppendLine();
            if (indent.Length < 2 && ownerLookup.TryGetComponent(e, out var owner))
            {
                sb.AppendLine($"{indent}\tOwner {GetName(owner.m_Owner)}\n{ToDebugString(owner.m_Owner, indent + "\t")}");
            }
            if (indent.Length < 2 && prefabRefLookup.TryGetComponent(e, out var prefab))
            {
                sb.AppendLine($"{indent}\tPrefab\n{ToDebugString(prefab.m_Prefab, indent + "\t")}");
            }
            if (indent.Length < 2 && aggregatedLookup.TryGetComponent(e, out var aggregated))
            {
                sb.AppendLine($"{indent}\tAggregated\n{ToDebugString(aggregated.m_Aggregate, indent + "\t")}");
            }
            if (routeLaneLookup.TryGetComponent(e, out var routeLane))
            {
                sb.AppendLine($"{indent}\tRouteLaneS:{GetName(routeLane.m_StartLane)}\n{ToDebugString(routeLane.m_StartLane, indent + "\t")}");
                sb.AppendLine($"{indent}\tRouteLaneE:{GetName(routeLane.m_EndLane)}\n{ToDebugString(routeLane.m_EndLane, indent + "\t")}");
            }
            return sb.ToString();
        }

        public void Execute()
        {
            FetchEntries(citizen);
            if (resultEntries.IsEmpty && currentVehicleLookup.TryGetComponent(citizen, out var currentVehicle))
            {
                FetchEntries(currentVehicle.m_Vehicle);
            }
        }

        public void FetchEntries(Entity subject)
        {
            if (!IsValidEntity(subject))
            {
                return;
            }

            {
                if (humanLaneLookup.TryGetComponent(subject, out HumanCurrentLane humanLane) && curveLookup.TryGetComponent(humanLane.m_Lane, out Curve humanCurve))
                {
                    AppendNavigationEntry(humanLane.m_Lane, humanLane.m_CurvePosition, humanCurve);
                }
                if (carNavigationLaneSegmentLookup.TryGetBuffer(subject, out DynamicBuffer<CarNavigationLane> pathElements))
                {
                    foreach (var pathElement in pathElements)
                    {
                        if (curveLookup.TryGetComponent(pathElement.m_Lane, out Curve curve))
                        {
                            AppendNavigationEntry(pathElement.m_Lane, pathElement.m_CurvePosition, curve);
                        }
                    }
                }
                if (carLaneLookup.TryGetComponent(subject, out CarCurrentLane carLane) && curveLookup.TryGetComponent(carLane.m_Lane, out Curve carCurve))
                {
                    AppendNavigationEntry(carLane.m_Lane, carLane.m_CurvePosition.xz, carCurve);
                }
            }
            if (pathOwnerLookup.TryGetComponent(subject, out PathOwner pathOwner))
            {
                var currentPathElementIndex = pathOwner.m_ElementIndex;
                if (pathElementLookup.TryGetBuffer(subject, out DynamicBuffer<PathElement> pathElements))
                {
                    for (int i = currentPathElementIndex; i < pathElements.Length; i++)
                    {
                        PathElement element = pathElements[i];
                        if (waypointLookup.TryGetComponent(element.m_Target, out var waypointStart))
                        {
                            int j;
                            for (j = i + 1; j < pathElements.Length; j++)
                            {
                                if (waypointLookup.TryGetComponent(pathElements[j].m_Target, out var waypointEnd))
                                {
                                    Entity? vehicle = default;
                                    if (i == currentPathElementIndex && currentVehicleLookup.TryGetComponent(subject, out var currentVehicle))
                                    {
                                        vehicle = currentVehicle.m_Vehicle;
                                    }
                                    AppendNavigationEntry(element, waypointStart, waypointEnd, vehicle);
                                    break;
                                }
                            }
                            if (j != pathElements.Length)
                            {
                                i = j;
                            }
                        }
                        else if (curveLookup.TryGetComponent(element.m_Target, out var curve))
                        {
                            AppendNavigationEntry(element.m_Target, element.m_TargetDelta, curve);
                        }
                    }
                }
            }
        }

        private void AppendNavigationEntry(in Entity target, in float2 delta, in Curve curve)
        {
            Entity nameEntity, subNameEntity;
            bool isPrimary;
            var targetAncestor = GetAncestor(target);
            if (ownerLookup.TryGetComponent(target, out var owner) &&
                aggregatedLookup.TryGetComponent(owner.m_Owner, out var aggregated))
            {
                if (buildingLookup.HasComponent(targetAncestor))
                {
                    nameEntity = targetAncestor;
                    subNameEntity = aggregated.m_Aggregate;
                }
                else
                {
                    nameEntity = aggregated.m_Aggregate;
                    subNameEntity = target;
                }
                isPrimary = true;
            }
            else
            {
                nameEntity = targetAncestor;
                subNameEntity = target;
                isPrimary = buildingLookup.HasComponent(nameEntity);
            }

            float remaining = MathUtils.Length(curve.m_Bezier, new Bounds1(delta));
            if (resultEntries.Length > 0 && resultEntries[resultEntries.Length - 1].Type == Game.Prefabs.TransportType.None)
            {
                if (resultEntryPaths.Length > 0 && resultEntryPaths[resultEntryPaths.Length - 1].NameEntity == nameEntity)
                {
                    var prev = resultEntryPaths[resultEntryPaths.Length - 1];
                    prev.Distance += curve.m_Length;
                    prev.RemainingDistance += remaining;
                    resultEntryPaths[resultEntryPaths.Length - 1] = prev;
                }
                else
                {
                    resultEntryPaths.Add(new NativeNavigationEntryPath
                    {
                        NameEntity = nameEntity,
                        SubNameEntity = subNameEntity,
                        IsPrimary = isPrimary,
                        Distance = curve.m_Length,
                        RemainingDistance = remaining,
                    });
                    var prev = resultEntries[resultEntries.Length - 1];
                    prev.PathEndIndex = resultEntryPaths.Length;
                    resultEntries[resultEntries.Length - 1] = prev;
                }
            }
            else
            {
                resultEntryPaths.Add(new NativeNavigationEntryPath
                {
                    NameEntity = nameEntity,
                    SubNameEntity = subNameEntity,
                    IsPrimary = isPrimary,
                    Distance = curve.m_Length,
                    RemainingDistance = remaining,
                });
                resultEntries.Add(new NativeNavigationEntry
                {
                    Type = Game.Prefabs.TransportType.None,
                    NameEntity = default,
                    PathBeginIndex = resultEntryPaths.Length - 1,
                    PathEndIndex = resultEntryPaths.Length,
                });

            }
        }

        private void AppendNavigationEntry(in PathElement element, Waypoint start, in Waypoint end, in Entity? vehicle)
        {
            if (ownerLookup.TryGetComponent(element.m_Target, out var owner) && 
                prefabRefLookup.TryGetComponent(owner.m_Owner, out var routePrefabRef) && 
                colorLookup.TryGetComponent(owner.m_Owner, out var routeColor) &&
                transportLineDataLookup.TryGetComponent(routePrefabRef.m_Prefab, out var transportLineData) && 
                routeSegmentLookup.TryGetBuffer(owner.m_Owner, out var routeSegments) &&
                routeWaypointLookup.TryGetBuffer(owner.m_Owner, out var routeWaypoints))
            {
                var currentEntry = new NativeNavigationEntry
                {
                    Type = transportLineData.m_TransportType,
                    Color = routeColor.m_Color,
                    NameEntity = GetAncestor(owner.m_Owner),
                    PathBeginIndex = resultEntryPaths.Length,
                };
                {
                    if (vehicle is Entity vehicle_)
                    {
                        if (controllerLookup.TryGetComponent(vehicle_, out var controller))
                        {
                            vehicle_ = controller.m_Controller;
                        }
                        if (targetLookup.TryGetComponent(vehicle_, out var target) && 
                            waypointLookup.TryGetComponent(target.m_Target, out var waypoint) &&
                            waypoint.m_Index != start.m_Index)
                        {
                            start.m_Index = (waypoint.m_Index + routeWaypoints.Length - 1) % routeWaypoints.Length;
                        }
                    }
                    var routeWaypoint = routeWaypoints[start.m_Index];
                    if (connectedLookup.TryGetComponent(routeWaypoint.m_Waypoint, out var connected))
                    {
                        // dummy path indicating starting waypoint
                        if (ownerLookup.TryGetComponent(connected.m_Connected, out var ownerStation))
                        {
                            resultEntryPaths.Add(new NativeNavigationEntryPath
                            {
                                SubNameEntity = connected.m_Connected,
                                NameEntity = GetAncestor(ownerStation.m_Owner)
                            });
                        }
                        else
                        {
                            resultEntryPaths.Add(new NativeNavigationEntryPath
                            {
                                SubNameEntity = connected.m_Connected,
                                NameEntity = GetAncestor(connected.m_Connected)
                            });
                        }
                    }
                }
                for (int i = start.m_Index; i != end.m_Index; i = (i + 1) % routeSegments.Length)
                {
                    var routeWaypoint = routeWaypoints[(i + 1) % routeWaypoints.Length];
                    var path = new NativeNavigationEntryPath();
                    if (i == start.m_Index &&
                        waitingPassengersLookup.TryGetComponent(routeWaypoint.m_Waypoint, out var waitingPassengers))
                    {
                        currentEntry.WaitingPassengers = waitingPassengers.m_Count;
                    }
                    if (connectedLookup.TryGetComponent(routeWaypoint.m_Waypoint, out var connected))
                    {
                        if (ownerLookup.TryGetComponent(connected.m_Connected, out var ownerStation))
                        {
                            path.SubNameEntity = connected.m_Connected;
                            path.NameEntity = GetAncestor(ownerStation.m_Owner);
                        }
                        else
                        {
                            path.SubNameEntity = connected.m_Connected;
                            path.NameEntity = GetAncestor(connected.m_Connected);
                        }
                    }
                    var routeSegment = routeSegments[i];
                    var distance = 0f;
                    var remainingDistance = 0f;
                    float3? vehiclePosition = null;
                    if (i == start.m_Index && vehicle is Entity vehicle_ && transformLookup.TryGetComponent(vehicle_, out var transform))
                    {
                        vehiclePosition = transform.m_Position;
                    }
                    if (pathElementLookup.TryGetBuffer(routeSegment.m_Segment, out DynamicBuffer<PathElement> trackCurves))
                    {
                        var minVehicleDistance = float.MaxValue;
                        foreach (var subElement in trackCurves)
                        {
                            if (curveLookup.TryGetComponent(subElement.m_Target, out Curve curve))
                            {
                                distance += curve.m_Length;
                                var length = MathUtils.Length(curve.m_Bezier, new Bounds1(subElement.m_TargetDelta));
                                if (vehiclePosition is float3 vehiclePosition_)
                                {
                                    var vehicleDistance = MathUtils.Distance(curve.m_Bezier, vehiclePosition_, out var t);
                                    if (vehicleDistance < minVehicleDistance)
                                    {
                                        minVehicleDistance = vehicleDistance;
                                        remainingDistance = MathUtils.Length(curve.m_Bezier, new Bounds1(Mathf.Min(t, subElement.m_TargetDelta.y), subElement.m_TargetDelta.y));
                                        continue;
                                    }
                                }
                                remainingDistance += length;
                            }
                        }
                    }

                    path.Distance = distance;
                    path.RemainingDistance = remainingDistance;
                    resultEntryPaths.Add(path);
                }
                currentEntry.PathEndIndex = resultEntryPaths.Length;
                resultEntries.Add(currentEntry);
            }
        }

        private bool IsValidEntity(in Entity e)
        {
            return storageInfoLookup.Exists(e) && !deletedLookup.HasComponent(e);
        }

        private float3? GetVehicleLanePosition(in Entity transportVehicle)
        {
            if (carLaneLookup.TryGetComponent(transportVehicle, out var carCurrentLane) && curveLookup.TryGetComponent(carCurrentLane.m_Lane, out var curve))
            {
                return MathUtils.Position(curve.m_Bezier, carCurrentLane.m_CurvePosition.x);
            }
            else if (trainCurrentLaneLookup.TryGetComponent(transportVehicle, out var trainCurrentLane) && curveLookup.TryGetComponent(trainCurrentLane.m_Front.m_Lane, out curve))
            {
                return MathUtils.Position(curve.m_Bezier, trainCurrentLane.m_Front.m_CurvePosition.y);
            }
            else if (watercraftCurrentLaneLookup.TryGetComponent(transportVehicle, out var watercraftCurrentLane) && curveLookup.TryGetComponent(watercraftCurrentLane.m_Lane, out curve))
            {
                return MathUtils.Position(curve.m_Bezier, watercraftCurrentLane.m_CurvePosition.x);
            }
            else if (aircraftCurrentLaneLookup.TryGetComponent(transportVehicle, out var aircraftCurrentLane) && curveLookup.TryGetComponent(aircraftCurrentLane.m_Lane, out curve))
            {
                return MathUtils.Position(curve.m_Bezier, aircraftCurrentLane.m_CurvePosition.x);
            }
            return null;
        }

        // From LineVisualizerSection
        private void GetVehicleRemaining(in Entity transportVehicle, out float distanceToWaypoint, out HashSet<Entity> reachedPathElementTargets)
        {
            distanceToWaypoint = 0f;
            if (carLaneLookup.TryGetComponent(transportVehicle, out var carCurrentLane) && curveLookup.TryGetComponent(carCurrentLane.m_Lane, out var curve))
            {
                distanceToWaypoint += MathUtils.Length(curve.m_Bezier, new Bounds1(carCurrentLane.m_CurvePosition.xz));
            }
            else if (trainCurrentLaneLookup.TryGetComponent(transportVehicle, out var trainCurrentLane) && curveLookup.TryGetComponent(trainCurrentLane.m_Front.m_Lane, out curve))
            {
                var len = MathUtils.Length(curve.m_Bezier, new Bounds1(trainCurrentLane.m_Front.m_CurvePosition.yw));
                distanceToWaypoint += len;
                Mod.log.Info($"{transportVehicle} has CURRENT lane {GetName(trainCurrentLane.m_Front.m_Lane)} of {len}");
                distanceToWaypoint += MathUtils.Length(curve.m_Bezier, new Bounds1(trainCurrentLane.m_Front.m_CurvePosition.yw));
            }
            else if (watercraftCurrentLaneLookup.TryGetComponent(transportVehicle, out var watercraftCurrentLane) && curveLookup.TryGetComponent(watercraftCurrentLane.m_Lane, out curve))
            {
                distanceToWaypoint += MathUtils.Length(curve.m_Bezier, new Bounds1(watercraftCurrentLane.m_CurvePosition.xz));
            }
            else if (aircraftCurrentLaneLookup.TryGetComponent(transportVehicle, out var aircraftCurrentLane) && curveLookup.TryGetComponent(aircraftCurrentLane.m_Lane, out curve))
            {
                distanceToWaypoint += MathUtils.Length(curve.m_Bezier, new Bounds1(aircraftCurrentLane.m_CurvePosition.xz));
            }
            if (carNavigationLaneSegmentLookup.TryGetBuffer(transportVehicle, out var carNavigationLaneBuffer))
            {
                foreach (var carNavigationLane in carNavigationLaneBuffer)
                {
                    if (curveLookup.TryGetComponent(carNavigationLane.m_Lane, out curve))
                    {
                        distanceToWaypoint += MathUtils.Length(curve.m_Bezier, new Bounds1(carNavigationLane.m_CurvePosition));
                    }
                }
            }
            else if (trainNavigationLaneLookup.TryGetBuffer(transportVehicle, out var trainNavigationLaneBuffer))
            {
                foreach (var trainNavigationLane in trainNavigationLaneBuffer)
                {
                    if (curveLookup.TryGetComponent(trainNavigationLane.m_Lane, out curve))
                    {
                        var len = MathUtils.Length(curve.m_Bezier, new Bounds1(trainNavigationLane.m_CurvePosition));
                        distanceToWaypoint += len;
                        Mod.log.Info($"{transportVehicle} has NAV lane {GetName(trainNavigationLane.m_Lane)} of {len}");
                    }
                }
            }
            else if (watercraftNavigationLaneLookup.TryGetBuffer(transportVehicle, out var watercraftNavigationLaneBuffer))
            {
                foreach (var watercraftNavigationLane in watercraftNavigationLaneBuffer)
                {
                    if (curveLookup.TryGetComponent(watercraftNavigationLane.m_Lane, out curve))
                    {
                        distanceToWaypoint += MathUtils.Length(curve.m_Bezier, new Bounds1(watercraftNavigationLane.m_CurvePosition));
                    }
                }
            }
            else if (aircraftNavigationLaneLookup.TryGetBuffer(transportVehicle, out var aircraftNavigationLaneBuffer))
            {
                foreach (var aircraftNavigationLane in aircraftNavigationLaneBuffer)
                {
                    if (curveLookup.TryGetComponent(aircraftNavigationLane.m_Lane, out curve))
                    {
                        distanceToWaypoint += MathUtils.Length(curve.m_Bezier, new Bounds1(aircraftNavigationLane.m_CurvePosition));
                    }
                }
            }
            reachedPathElementTargets = new HashSet<Entity>();
            if (pathElementLookup.TryGetBuffer(transportVehicle, out var pathElementBuffer))
            {
                foreach (var pathElement in pathElementBuffer)
                {
                    if (curveLookup.TryGetComponent(pathElement.m_Target, out curve))
                    {
                        distanceToWaypoint += MathUtils.Length(curve.m_Bezier, new Bounds1(pathElement.m_TargetDelta));
                        reachedPathElementTargets.Add(pathElement.m_Target);
                    }
                }
            }
        }
    }
}
