import "./styles/tokens.css";
import "./style.css";
import "./recipeList.css";
import { initTheme } from "./theme";
import { dom, buildLayoutDom, ROUTE, MANAGE_ACTIVE, DEPENDENCIES_ACTIVE, ASSIGNMENT_ACTIVE, CUSTOM_RECIPES_ACTIVE, BURGER_MAKER_ACTIVE, FILLING_MAKER_ACTIVE, GUIDE_ACTIVE, GUIDE_PAGE_ID } from "./editor/dom";
import { init } from "./editor/init";
import { setRedraw } from "./editor/iconCaches";
import { setRefreshHooks, draw } from "./editor/render";

initTheme();
document.addEventListener("oc2-theme-change", () => {
  if (document.getElementById("canvas")) draw();
});
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
  void import("./guide").then((m) => m.renderGuideView(dom.app, GUIDE_PAGE_ID));
} else if (CUSTOM_RECIPES_ACTIVE) {
  document.body.classList.add("manage-bg");
  void import("./customRecipes").then(m => m.renderCustomRecipesView(dom.app));
} else if (BURGER_MAKER_ACTIVE) {
  document.body.classList.add("manage-bg");
  void import("./burgerMaker").then((m) => m.renderBurgerMakerView(dom.app));
} else if (FILLING_MAKER_ACTIVE) {
  document.body.classList.add("manage-bg");
  void import("./fillingMaker").then((m) => m.renderFillingMakerView(dom.app));
} else if (DEPENDENCIES_ACTIVE) {
  void import("./dependencies").then((m) => m.renderDependenciesView(dom.app));
} else if (ASSIGNMENT_ACTIVE) {
  // 关卡级严格路由 /assignment/{set}/{levelId}（菜谱分工模式页）
  document.body.classList.add("manage-bg");
  void import("./levelAssignment").then((m) => m.renderAssignmentRouteView(dom.app));
} else if (MANAGE_ACTIVE) {
  void renderManageView(dom.app);
} else {
  // 严格路由 /layout/{set}/{scene}：裸 /layout 一律回关卡管理（迁移函数已把旧 ?scene= 换成严格路径）
  if (ROUTE.setId && ROUTE.sceneName) {
    void init();
  } else {
    goManage();
  }
}
