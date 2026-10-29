import { decorModelInfo } from "../decorPreviewCache";
import { openDecorPreviewLightbox } from "./decorPreviewLightbox";

/** 打开装饰物统一预览并直接切到 3D 页签；模型未导出时返回 false。 */
export function openDecor3dPreview(guid: string, label: string): boolean {
  if (!decorModelInfo(guid)) return false;
  openDecorPreviewLightbox(guid, label, "3d");
  return true;
}
