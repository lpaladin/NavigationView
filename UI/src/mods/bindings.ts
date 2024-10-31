import { bindValue, trigger } from "cs2/api";
import mod from "mod.json";
import { NavigationEntry } from "./types";

export const navigationEntries$ = bindValue<NavigationEntry[]>(mod.id, "navigationEntries");
export const navigationViewEnabled$ = bindValue<boolean>(mod.id, "enabled");
export const navigationViewToggle = (to: boolean) => trigger(mod.id, "toggle", to);
