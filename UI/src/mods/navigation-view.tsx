import { useValue } from "cs2/api";
import { Name } from "cs2/bindings";
import { LocalizedEntityName, LocalizedNumber, Unit, useLocalization } from "cs2/l10n";
import { Icon, Panel } from "cs2/ui";
import mod from "mod.json";
import { navigationEntries$, navigationViewEnabled$, navigationViewToggle } from "mods/bindings";
import { ReactNode, useCallback } from "react";
import styles from "./navigation-view.module.scss";
import { NavigationEntry, NavigationEntryPath } from "./types";

function getDisplayColor(color: string): string {
    if (color.endsWith("00")) {
        return "#888e";
    }
    return `#${color}`;
}

function isValidName(name: Name): boolean {
    if ("name" in name) {
        return name.name?.length > 0;
    }
    return name.nameId?.length > 0;
}

const PathDetails = ({ path }: { path: NavigationEntryPath }) => {
    let distanceElement: ReactNode = null;
    if (path.remainingDistance > 1e-6) {
        distanceElement = <div className={ styles.pathDistance }>
            <LocalizedNumber value={ path.remainingDistance } unit={ Unit.Length } />
        </div>;
    }
    return <div className={ styles.pathDetails }>
        <div className={ styles.pathName }><LocalizedEntityName value={ path.name } /></div>
        { isValidName(path.subName) && <div className={ styles.pathSubName }><LocalizedEntityName value={ path.subName } /></div> }
        { distanceElement }
    </div>;
};

const NavigationViewEntry = ({ entry }: { entry: NavigationEntry }) => {
    const pathIndices = [];
    let routeNameAddon: ReactNode = null;
    if (entry.paths[0].remainingDistance < 1e-6) {
        for (let i = 1; i < entry.paths.length; i++) {
            if (entry.paths[i].remainingDistance > 1e-6) {
                pathIndices.push(i);
            }
        }
        routeNameAddon = <PathDetails path={ entry.paths[0] } />;
    } else {
        for (let i = 0; i < entry.paths.length; i++) {
            pathIndices.push(i);
        }
    }
    if (pathIndices.length === 0) {
        return null;
    }
    return <div className={ styles.entry }>
        <div className={ styles.routeDetails }>
            <div className={ styles.routeHeader }>
                <div className={ styles.routeContainer }>
                    <div className={ styles.line } style={ { backgroundColor: getDisplayColor(entry.color) } }></div>
                    <Icon className={ styles.routeIcon } src={ entry.icon } />
                </div>
                <div className={ styles.routeName } style={ { backgroundColor: getDisplayColor(entry.color) } }>
                    <LocalizedEntityName value={ entry.name } />
                </div>
                { routeNameAddon }
            </div>
            { pathIndices.map((i) => <div className={ styles.path }>
                <div className={ styles.routeContainer }>
                    <div className={ styles.line } style={ { backgroundColor: getDisplayColor(entry.color) } }></div>
                    <div className={ styles.pathIndicator } style={ { borderColor: getDisplayColor(entry.color) } }></div>
                </div>
                <PathDetails path={ entry.paths[i] } />
            </div>) }
        </div>
    </div>;
};

export const NavigationView = () => {
    const enabled = useValue(navigationViewEnabled$);
    const entries = useValue(navigationEntries$);
    const { translate } = useLocalization();
    const onClose = useCallback(() => navigationViewToggle(false), []);

    if (!enabled || !entries || entries.length === 0) {
        return null;
    }
    return <Panel
        className={ styles.panel }
        draggable
        header={ translate(`${mod.id}.UI.Title`)! }
        initialPosition={ { x: 0.95, y: 0.3 } }
        onClose={ onClose }
    >
        { entries.map((entry) => <NavigationViewEntry entry={ entry } />) }
    </Panel>;
}