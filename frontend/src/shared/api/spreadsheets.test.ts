import { beforeEach, describe, expect, it, vi } from "vitest";

const { postMock, getMock } = vi.hoisted(() => ({
  postMock: vi.fn(),
  getMock: vi.fn()
}));

vi.mock("./client", () => ({
  apiClient: {
    post: postMock,
    get: getMock
  }
}));

import {
  runQuery,
  runQueryDownload,
  runQueryPreview,
  uploadSpreadsheet,
  validateLinq
} from "./spreadsheets";

describe("spreadsheets api", () => {
  beforeEach(() => {
    postMock.mockReset();
    getMock.mockReset();
  });

  it("uploads spreadsheet as multipart/form-data", async () => {
    const file = new File(["a,b\n1,2\n"], "sample.csv", { type: "text/csv" });
    postMock.mockResolvedValue({ data: { jobId: "job-1" } });
    getMock
      .mockResolvedValueOnce({ data: { jobId: "job-1", stage: "Ready", progress: 100 } })
      .mockResolvedValueOnce({
        data: {
          variables: ["rows"],
          allowedMethods: [],
          snippets: [],
          sheets: [
            { sheetName: "sheet1", columns: ["nome"], rowCount: 2, previewRows: [{ nome: "Ana" }, { nome: "Bruno" }] }
          ]
        }
      });

    const result = await uploadSpreadsheet(file);

    expect(result.fileToken).toBe("job-1");
    expect(result.columns).toHaveLength(1);
    expect(result.columns.at(0)?.normalizedName).toBe("nome");
    expect(result.preview.rowCountPreview).toBe(2);
    expect(result.preview.rows).toEqual([{ nome: "Ana" }, { nome: "Bruno" }]);
    expect(postMock).toHaveBeenCalledTimes(1);

    const firstCall = postMock.mock.calls.at(0);
    expect(firstCall).toBeDefined();

    const [url, body, config] = firstCall as [string, FormData, { headers: { "Content-Type": string } }];
    expect(url).toBe("/api/query/upload");
    expect(config).toEqual({
      headers: { "Content-Type": "multipart/form-data" }
    });

    expect(body.get("file1")).toBe(file);
  });

  it("validates linq payload", async () => {
    const payload = { fileToken: "abc", linqCode: "rows.Where(row => true)" };
    postMock.mockResolvedValue({ data: { diagnostics: [] } });

    const result = await validateLinq(payload);

    expect(result).toEqual({ diagnostics: [] });
    expect(postMock).toHaveBeenCalledWith("/api/spreadsheets/validate", payload);
  });

  it("runs query preview", async () => {
    const payload = { fileToken: "abc", linqCode: "rows.Where(row => true)", outputFormat: "csv" as const };
    postMock.mockResolvedValue({ data: { queryId: "q2" } });
    getMock.mockResolvedValue({ data: { stage: "Completed", rows: [{ Nome: "Ana" }], elapsedMs: 12 } });

    const result = await runQueryPreview(payload);

    expect(result).toEqual({ rows: [{ Nome: "Ana" }], rowCountPreview: 1, elapsedMs: 12 });
    expect(postMock).toHaveBeenCalledWith("/api/query/abc/execute", {
      code: payload.linqCode,
      maxRows: 2000,
      timeoutMs: 4000
    });
  });

  it("downloads query output and infers filename from content-disposition", async () => {
    const payload = { fileToken: "abc", linqCode: "rows.Where(row => true)", outputFormat: "xlsx" as const };
    postMock.mockResolvedValue({ data: { queryId: "q3" } });
    getMock.mockResolvedValue({ data: { stage: "Completed", rows: [{ Nome: "Ana" }] } });

    const result = await runQueryDownload(payload);

    expect(result.blob).toBeInstanceOf(Blob);
    expect(result.fileName).toBe("linq-result.xlsx");
  });

  it("falls back to default filename when content-disposition is missing", async () => {
    const payload = { fileToken: "abc", linqCode: "rows.Where(row => true)", outputFormat: "csv" as const };
    postMock.mockResolvedValue({ data: { queryId: "q4" } });
    getMock.mockResolvedValue({ data: { stage: "Completed", rows: [] } });

    const result = await runQueryDownload(payload);

    expect(result.fileName).toBe("linq-result.csv");
  });

  it("returns empty preview when runQueryPreview fails", async () => {
    const payload = { fileToken: "abc", linqCode: "rows.Where(row => true)", outputFormat: "csv" as const };
    postMock.mockRejectedValue(new Error("boom"));

    const result = await runQuery(payload);

    expect(result).toEqual({ rows: [], rowCountPreview: 0 });
  });
});

