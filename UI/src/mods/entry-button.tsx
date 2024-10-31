import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { FloatingButton, Tooltip } from "cs2/ui";
import mod from "mod.json";
import iconSrc from "../images/icon.svg";
import { navigationViewEnabled$, navigationViewToggle } from "./bindings";

export const EntryButton = () => {
  const navigationViewEnabled = useValue(navigationViewEnabled$);
  const { translate } = useLocalization();
  return (
    <Tooltip tooltip={ translate(`${mod.id}.UI.Title`) }>
      <FloatingButton
        src={ iconSrc }
        selected={ navigationViewEnabled }
        onSelect={ () => navigationViewToggle(!navigationViewEnabled) }
      />
    </Tooltip>
  );
};