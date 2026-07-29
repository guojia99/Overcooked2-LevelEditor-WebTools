/** Strip manual-table variant counts: "城市砖墙 ×6" → "城市砖墙". */
export function tidyCatalogNameZh(name: string): string {
  return name.replace(/\s*[×xX]\d+\s*$/u, "").trim();
}
