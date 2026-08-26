import "./style.css";
import "./recipeList.css";
import { dom, buildLayoutDom, MANAGE_ACTIVE, CUSTOM_RECIPES_ACTIVE, GUIDE_ACTIVE } from "./editor/dom";
import { init } from "./editor/init";
import { setRedraw } from "./editor/iconCaches";
import { setRefreshHooks, draw } from "./editor/render";
import { updateFloorBar } from "./editor/floorPalette";
import { maybeRefreshSceneItemList } from "./editor/panels";
import { goManage, renderManageView } from "./levels";
import { mountVersionBadge } from "./version";

mountVersionBadge();
buildLayoutDom();
setRedraw(draw);
setRefreshHooks(() => {
  updateFloorBar();
  maybeRefreshSceneItemList();
});

if (GUIDE_ACTIVE) {
  void import("./guide").then((m) => m.renderGuideView(dom.app));
} else if (CUSTOM_RECIPES_ACTIVE) {
  document.body.classList.add("manage-bg");
  void import("./customRecipes").then(m => m.renderCustomRecipesView(dom.app));
} else if (MANAGE_ACTIVE) {
  void renderManageView(dom.app);
} else {
  const urlScene = new URLSearchParams(location.search).get("scene") ?? "";
  const hasTarget = !!sessionStorage.getItem("layoutTargetScene");
  if (!hasTarget && !urlScene) {
    goManage();
  } else {
    void init();
  }
}
