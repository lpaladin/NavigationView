import { Entity, Name } from "cs2/bindings";

interface NavigationEntryPath {
    name: Name;
    nameEntity: Entity;
    subName: Name;
    isPrimary: boolean;
    distance: number;
    remainingDistance: number;
    waitingPassengers: number;
}

interface NavigationEntry {
    type: string;
    name: Name;
    nameEntity: Entity;
    icon: string;
    color: string;
    paths: NavigationEntryPath[];
}
