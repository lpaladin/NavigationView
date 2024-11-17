using Colossal.UI.Binding;
using Game.Prefabs;
using Game.Routes;
using Game.UI;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace NavigationView
{

    public struct NativeNavigationEntry
    {
        public TransportType Type;
        public Entity NameEntity;
        public Color32 Color;
        public int PathBeginIndex;
        public int PathEndIndex;
    }

    public struct NativeNavigationEntryPath
    {
        public Entity NameEntity;
        public Entity SubNameEntity;
        public bool IsPrimary;
        public float Distance;
        public float RemainingDistance;
        public int WaitingPassengers;
    }

    public struct NavigationEntry
    {
        public struct Path
        {
            public Entity PathNameEntity;
            public Entity PathSubNameEntity;
            public bool IsPrimary;
            public float Distance;
            public float RemainingDistance;
            public int WaitingPassengers;
        }

        public TransportType Type;
        public Entity NameEntity;
        public string Color;
        public Path[] Paths;

        public static NavigationEntry[] FromNative(
            in NativeList<NativeNavigationEntry> nativeNavigationEntries, in NativeList<NativeNavigationEntryPath> nativeNavigationEntryPaths)
        {
            var entries = new NavigationEntry[nativeNavigationEntries.Length];
            for (int i = 0; i < nativeNavigationEntries.Length; i++)
            {
                var nativeEntry = nativeNavigationEntries[i];
                var entry = new NavigationEntry
                {
                    Type = nativeEntry.Type,
                    NameEntity = nativeEntry.NameEntity,
                    Color = ColorUtility.ToHtmlStringRGBA(nativeEntry.Color),
                    Paths = new Path[nativeEntry.PathEndIndex - nativeEntry.PathBeginIndex],
                };
                // for each unique name entity, find their first and last index, and merge all paths in between, sum their distances and remaining distances
                var primaryPathLastIndices = new Dictionary<Entity, int>();

                // 1: find the last index of each primary path
                for (int j = nativeEntry.PathBeginIndex; j < nativeEntry.PathEndIndex; j++)
                {
                    if (nativeNavigationEntryPaths[j].IsPrimary)
                    {
                        if (entry.NameEntity == default)
                        {
                            entry.NameEntity = nativeNavigationEntryPaths[j].NameEntity;
                        }
                        primaryPathLastIndices[nativeNavigationEntryPaths[j].NameEntity] = j;
                    }
                }

                if (entry.NameEntity == default)
                {
                    entry.NameEntity = nativeNavigationEntryPaths[nativeEntry.PathBeginIndex].NameEntity;
                }

                var paths = new List<Path>();
                // 2: merge all paths in between any two primary paths
                for (int j = nativeEntry.PathBeginIndex; j < nativeEntry.PathEndIndex; j++)
                {
                    var path = nativeNavigationEntryPaths[j];
                    if (primaryPathLastIndices.TryGetValue(path.NameEntity, out var lastPrimaryIndex))
                    {
                        var distance = 0f;
                        var remainingDistance = 0f;
                        for (int k = j; k <= lastPrimaryIndex; k++)
                        {
                            distance += nativeNavigationEntryPaths[k].Distance;
                            remainingDistance += nativeNavigationEntryPaths[k].RemainingDistance;
                        }
                        paths.Add(new Path
                        {
                            PathNameEntity = path.NameEntity,
                            PathSubNameEntity = path.NameEntity != path.SubNameEntity ? path.SubNameEntity : default,
                            IsPrimary = path.IsPrimary,
                            Distance = distance,
                            RemainingDistance = remainingDistance,
                            WaitingPassengers = path.WaitingPassengers,
                        });
                        j = lastPrimaryIndex;
                    }
                    else
                    {
                        paths.Add(new Path
                        {
                            PathNameEntity = path.NameEntity,
                            PathSubNameEntity = path.NameEntity != path.SubNameEntity ? path.SubNameEntity : default,
                            IsPrimary = path.IsPrimary,
                            Distance = path.Distance,
                            RemainingDistance = path.RemainingDistance,
                            WaitingPassengers = path.WaitingPassengers,
                        });
                    }
                    // Try coalesce the previous 2 paths if one of them is not primary
                    if (paths.Count > 1)
                    {
                        var prev = paths[paths.Count - 2];
                        var curr = paths[paths.Count - 1];
                        if (!prev.IsPrimary && curr.IsPrimary)
                        {
                            // merge prev into curr
                            curr.Distance += prev.Distance;
                            curr.RemainingDistance += prev.RemainingDistance;
                            paths.RemoveAt(paths.Count - 2);
                        }
                        else if (prev.IsPrimary && !curr.IsPrimary)
                        {
                            // merge curr into prev
                            prev.Distance += curr.Distance;
                            prev.RemainingDistance += curr.RemainingDistance;
                            paths.RemoveAt(paths.Count - 1);
                        }
                    }
                }
                entry.Paths = paths.ToArray();
                entries[i] = entry;
            }
            return entries;
        }
    }

    public class NavigationViewEntriesWriter : IWriter<NavigationEntry[]>
    {
        public NameSystem nameSystem;
        public ImageSystem imageSystem;

        private void BindEntity(IJsonWriter writer, in Entity entity)
        {
            writer.TypeBegin(nameof(Entity));
            writer.PropertyName("index");
            writer.Write(entity.Index);
            writer.PropertyName("version");
            writer.Write(entity.Version);
            writer.TypeEnd();
        }

        public void Write(IJsonWriter writer, NavigationEntry[] value)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }
            writer.ArrayBegin(value.Length);
            foreach (var entry in value)
            {
                writer.TypeBegin(nameof(NavigationEntry));
                writer.PropertyName("type");
                writer.Write(entry.Type.ToString());

                writer.PropertyName("color");
                writer.Write(entry.Color);

                writer.PropertyName("name");
                nameSystem.BindName(writer, entry.NameEntity);

                writer.PropertyName("nameEntity");
                BindEntity(writer, entry.NameEntity);

                writer.PropertyName("icon");
                writer.Write(imageSystem.GetInstanceIcon(entry.NameEntity));

                writer.PropertyName("paths");
                writer.ArrayBegin(entry.Paths.Length);
                foreach (var path in entry.Paths)
                {
                    writer.TypeBegin(nameof(NavigationEntry.Path));
                    writer.PropertyName("name");
                    nameSystem.BindName(writer, path.PathNameEntity);

                    writer.PropertyName("nameEntity");
                    BindEntity(writer, path.PathNameEntity);

                    writer.PropertyName("subName");
                    nameSystem.BindName(writer, path.PathSubNameEntity);

                    writer.PropertyName("isPrimary");
                    writer.Write(path.IsPrimary);

                    writer.PropertyName("distance");
                    writer.Write(path.Distance);

                    writer.PropertyName("remainingDistance");
                    writer.Write(path.RemainingDistance);

                    writer.PropertyName("waitingPassengers");
                    writer.Write(path.WaitingPassengers);

                    writer.TypeEnd();
                }
                writer.ArrayEnd();

                writer.TypeEnd();
            }
            writer.ArrayEnd();
        }
    }
}