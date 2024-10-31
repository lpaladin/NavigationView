import { ModRegistrar } from "cs2/modding";
import { EntryButton } from "mods/entry-button";
import { NavigationView } from "mods/navigation-view";

const register: ModRegistrar = (moduleRegistry) => {
    moduleRegistry.append('GameTopLeft', EntryButton);
    moduleRegistry.append('Game', NavigationView);
}

export default register;