import { apiClient } from "./client";
import type { SavedQuery } from "./types";

const SAVED_QUERIES_PATH = "/api/saved-queries";

interface CreateSavedQueryRequest {
  name: string;
  linqCode: string;
}

export async function listSavedQueries(): Promise<SavedQuery[]> {
  const response = await apiClient.get<SavedQuery[]>(SAVED_QUERIES_PATH);
  return response.data;
}

export async function getSavedQueryById(id: number): Promise<SavedQuery> {
  const response = await apiClient.get<SavedQuery>(`${SAVED_QUERIES_PATH}/${id}`);
  return response.data;
}

export async function createSavedQuery(payload: CreateSavedQueryRequest): Promise<SavedQuery> {
  const response = await apiClient.post<SavedQuery>(SAVED_QUERIES_PATH, payload);
  return response.data;
}

export async function deleteSavedQuery(id: number): Promise<void> {
  await apiClient.delete(`${SAVED_QUERIES_PATH}/${id}`);
}
