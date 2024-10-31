using Colossal.UI.Binding;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Creatures;
using Game.Net;
using Game.Pathfind;
using Game.Routes;
using Game.Tools;
using Game.UI;
using Game.Vehicles;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace NavigationView
{
    [BurstCompile]
    internal partial class NavigationRouteListSystem : UISystemBase
    {
        private ToolSystem toolSystem;
        public static NameSystem nameSystem; // debug only
        private ValueBinding<NavigationEntry[]> navigationEntries;
        private ValueBinding<bool> navigationViewEnabled;
        private TriggerBinding<bool> navigationViewToggle;
        private Entity currentEntity;
        private FetchNavigationEntriesJob currentJob;
        private JobHandle? currentJobHandle;
        private float lastStartedAt = 0;

        private Entity GetSelectedEntity()
        {
            return toolSystem.selected;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            Mod.log.Info("NavigationRouteListSystem created");
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            toolSystem = World.GetExistingSystemManaged<ToolSystem>();
            nameSystem = World.GetExistingSystemManaged<NameSystem>();
            var imageSystem = World.GetExistingSystemManaged<ImageSystem>();

            if (navigationEntries != null)
            {
                return;
            }

            navigationEntries = new ValueBinding<NavigationEntry[]>(nameof(NavigationView), "navigationEntries", null, new NavigationViewEntriesWriter { nameSystem = nameSystem, imageSystem = imageSystem });
            AddBinding(navigationEntries);

            navigationViewEnabled = new ValueBinding<bool>(nameof(NavigationView), "enabled", false);
            AddBinding(navigationViewEnabled);

            navigationViewToggle = new TriggerBinding<bool>(nameof(NavigationView), "toggle", to => navigationViewEnabled.Update(to));
            AddBinding(navigationViewToggle);

            Mod.log.Info("binding added");
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            Entity newEntity = GetSelectedEntity();
            if (newEntity != currentEntity)
            {
                Mod.log.Info($"New entity: {newEntity} vs {currentEntity}");
                currentEntity = newEntity;
                if (currentEntity == default)
                {
                    navigationEntries.Update(null);
                }
                else
                {
                    LaunchJob();
                }
            }
            else if (currentJobHandle?.IsCompleted == true)
            {
                currentJobHandle?.Complete();
                navigationEntries.Update(NavigationEntry.FromNative(currentJob.resultEntries, currentJob.resultEntryPaths));
                currentJob.resultEntries.Dispose();
                currentJob.resultEntryPaths.Dispose();
                currentJob = default;
                currentJobHandle = null;
            }
            else if (currentJobHandle == null)
            {
                if (Mod.Instance.Setting.RefreshFrequency > 0 && UnityEngine.Time.time - lastStartedAt > 1f / Mod.Instance.Setting.RefreshFrequency)
                {
                    LaunchJob();
                }
            }
        }

        private void LaunchJob()
        {
            lastStartedAt = UnityEngine.Time.time;
            currentJob = new FetchNavigationEntriesJob
            {
                citizen = currentEntity,
                manager = EntityManager,
                pathOwnerLookup = GetComponentLookup<PathOwner>(true),
                ownerLookup = GetComponentLookup<Owner>(true),
                routeLaneLookup = GetComponentLookup<RouteLane>(true),
                waypointLookup = GetComponentLookup<Waypoint>(true),
                aggregatedLookup = GetComponentLookup<Aggregated>(true),
                pedestrianLaneLookup = GetComponentLookup<PedestrianLane>(true),
                humanLaneLookup = GetComponentLookup<HumanCurrentLane>(true),
                curveLookup = GetComponentLookup<Curve>(true),
                carLaneLookup = GetComponentLookup<CarCurrentLane>(true),
                trackLaneLookup = GetComponentLookup<TrackLane>(true),
                colorLookup = GetComponentLookup<Game.Routes.Color>(true),
                secondaryLaneLookup = GetComponentLookup<SecondaryLane>(true),
                currentVehicleLookup = GetComponentLookup<CurrentVehicle>(true),
                currentTransportLookup = GetComponentLookup<CurrentTransport>(true),
                connectedLookup = GetComponentLookup<Connected>(true),
                targetLookup = GetComponentLookup<Target>(true),
                deletedLookup = GetComponentLookup<Deleted>(true),
                prefabRefLookup = GetComponentLookup<Game.Prefabs.PrefabRef>(true),
                transportLineDataLookup = GetComponentLookup<Game.Prefabs.TransportLineData>(true),
                trainCurrentLaneLookup = GetComponentLookup<TrainCurrentLane>(true),
                watercraftCurrentLaneLookup = GetComponentLookup<WatercraftCurrentLane>(true),
                aircraftCurrentLaneLookup = GetComponentLookup<AircraftCurrentLane>(true),
                buildingLookup = GetComponentLookup<Building>(true),
                transformLookup = GetComponentLookup<Game.Objects.Transform>(true),
                controllerLookup = GetComponentLookup<Controller>(true),
                storageInfoLookup = GetEntityStorageInfoLookup(),
                pathElementLookup = GetBufferLookup<PathElement>(true),
                routeSegmentLookup = GetBufferLookup<RouteSegment>(true),
                carNavigationLaneSegmentLookup = GetBufferLookup<CarNavigationLane>(true),
                routeWaypointLookup = GetBufferLookup<RouteWaypoint>(true),
                trainNavigationLaneLookup = GetBufferLookup<TrainNavigationLane>(true),
                watercraftNavigationLaneLookup = GetBufferLookup<WatercraftNavigationLane>(true),
                aircraftNavigationLaneLookup = GetBufferLookup<AircraftNavigationLane>(true),
                resultEntries = new NativeList<NativeNavigationEntry>(Allocator.TempJob),
                resultEntryPaths = new NativeList<NativeNavigationEntryPath>(Allocator.TempJob)
            };
            currentJobHandle = currentJob.Schedule();
        }
    }
}
