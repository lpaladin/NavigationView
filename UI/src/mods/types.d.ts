import { Name } from "cs2/bindings";

interface NavigationEntryPath {
    name: Name;
    subName: Name;
    isPrimary: boolean;
    distance: number;
    remainingDistance: number;
}

interface NavigationEntry {
    type: string;
    name: Name;
    icon: string;
    color: string;
    paths: NavigationEntryPath[];
}
