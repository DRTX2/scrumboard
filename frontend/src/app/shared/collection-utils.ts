export function adjacentIds<T extends { id: string }>(items: T[], index: number): { beforeId: string | null; afterId: string | null } {
  return {
    beforeId: index < items.length - 1 ? items[index + 1].id : null,
    afterId: index > 0 ? items[index - 1].id : null
  };
}

export function normalizeArray<T>(response: T[] | { items?: T[]; data?: T[] }): T[] {
  if (Array.isArray(response)) return response;
  return response.items ?? response.data ?? [];
}
