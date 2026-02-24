import { beforeEach, describe, expect, it, vi } from "vitest";

const { getMock, postMock, deleteMock } = vi.hoisted(() => ({
  getMock: vi.fn(),
  postMock: vi.fn(),
  deleteMock: vi.fn()
}));

vi.mock("./client", () => ({
  apiClient: {
    get: getMock,
    post: postMock,
    delete: deleteMock
  }
}));

import {
  createSavedQuery,
  deleteSavedQuery,
  getSavedQueryById,
  listSavedQueries
} from "./saved-queries";

describe("saved queries api", () => {
  beforeEach(() => {
    getMock.mockReset();
    postMock.mockReset();
    deleteMock.mockReset();
  });

  it("lists saved queries", async () => {
    const payload = [{ id: 1, name: "Query A", linqCode: "rows.Where(r => true)", createdAtUtc: "2026-02-23T00:00:00Z", updatedAtUtc: "2026-02-23T00:00:00Z" }];
    getMock.mockResolvedValue({ data: payload });

    const result = await listSavedQueries();

    expect(result).toEqual(payload);
    expect(getMock).toHaveBeenCalledWith("/api/saved-queries");
  });

  it("gets query by id", async () => {
    const payload = { id: 2, name: "Query B", linqCode: "rows.Where(r => r.id > 10)", createdAtUtc: "2026-02-23T00:00:00Z", updatedAtUtc: "2026-02-23T00:00:00Z" };
    getMock.mockResolvedValue({ data: payload });

    const result = await getSavedQueryById(2);

    expect(result).toEqual(payload);
    expect(getMock).toHaveBeenCalledWith("/api/saved-queries/2");
  });

  it("creates query", async () => {
    const payload = { id: 3, name: "Query C", linqCode: "rows.Take(10)", createdAtUtc: "2026-02-23T00:00:00Z", updatedAtUtc: "2026-02-23T00:00:00Z" };
    postMock.mockResolvedValue({ data: payload });

    const result = await createSavedQuery({ name: "Query C", linqCode: "rows.Take(10)" });

    expect(result).toEqual(payload);
    expect(postMock).toHaveBeenCalledWith("/api/saved-queries", { name: "Query C", linqCode: "rows.Take(10)" });
  });

  it("deletes query", async () => {
    deleteMock.mockResolvedValue({ status: 204 });

    await deleteSavedQuery(3);

    expect(deleteMock).toHaveBeenCalledWith("/api/saved-queries/3");
  });
});
