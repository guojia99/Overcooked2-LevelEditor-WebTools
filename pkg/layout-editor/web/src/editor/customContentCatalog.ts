import { S } from "./state";
import { fetchCustomContentCatalog } from "../api";
import type { Catalog, CatalogItem } from "../types";

let activeCatalog: Catalog | null = null;
let lastSetId = "";
const dynamicGuids = new Set<string>();

export function setActiveCatalog(catalog: Catalog): void {
  activeCatalog = catalog;
}

export function getActiveCatalog(): Catalog | null {
  return activeCatalog;
}

function removeDynamic(catalog: Catalog): void {
  if (dynamicGuids.size === 0) return;
  catalog.items = catalog.items.filter((it) => !dynamicGuids.has(it.guid));
  for (const key of Object.keys(catalog.byCategory)) {
    const list = catalog.byCategory[key];
    if (!list) continue;
    catalog.byCategory[key] = list.filter((it) => !dynamicGuids.has(it.guid));
  }
  for (const guid of dynamicGuids) S.catalogByGuid.delete(guid);
  for (const [id, item] of S.catalogById) {
    if (dynamicGuids.has(item.guid)) S.catalogById.delete(id);
  }
  dynamicGuids.clear();
  catalog.itemCount = catalog.items.length;
}

/** 把当前关卡集的自定义内容并入静态 catalog；切换关卡集时先移除上一集的动态条目。 */
export async function mergeCustomContentIntoCatalog(catalog: Catalog, setId: string): Promise<void> {
  if (activeCatalog && activeCatalog !== catalog) activeCatalog = catalog;
  activeCatalog = catalog;
  if (lastSetId !== setId) removeDynamic(catalog);
  lastSetId = setId;
  try {
    const items = await fetchCustomContentCatalog(setId);
    for (const it of items) {
      if (!it.guid || !it.assetPath) continue;
      const cat: CatalogItem = {
        id: it.id,
        guid: it.guid,
        assetPath: it.assetPath,
        category: it.category || "other",
        theme: null,
        nameZh: it.nameZh || it.id,
        nameEn: it.nameEn || it.id,
        defaultParent: it.defaultParent || "Art",
        footprint: it.footprint ?? { cellsX: 1, cellsZ: 1 },
        layoutTier: "core",
        needsStub: !!it.needsStub,
        customStubKind: it.customStubKind,
        customKind: it.customKind,
      };
      if (!S.catalogByGuid.has(cat.guid)) {
        S.catalogByGuid.set(cat.guid, cat);
        catalog.items.push(cat);
        const list = catalog.byCategory[cat.category] ?? (catalog.byCategory[cat.category] = []);
        list.push(cat);
        dynamicGuids.add(cat.guid);
      }
      if (!S.catalogById.has(cat.id)) S.catalogById.set(cat.id, cat);
    }
    catalog.itemCount = catalog.items.length;
  } catch (e) {
    console.warn("自定义内容 catalog 合并失败", e);
  }
}
