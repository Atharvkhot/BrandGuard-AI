# BrandGuard AI &mdash; REST API Reference

Base URL: `http://localhost:5000/api`  
Swagger UI: `http://localhost:5000/swagger`

---

## 1. Brand Guidelines (`/api/guidelines`)

### 1.1 Upload Guideline
- **Endpoint:** `POST /api/guidelines/upload`
- **Content-Type:** `multipart/form-data`
- **Parameters:**
  - `file`: PDF document (binary, max 25MB)
  - `title` *(optional)*: Document display title
- **Response (201 Created):**
```json
{
  "id": "e53248c0-287e-4f46-95ce-72e45b26b248",
  "title": "Master Brand Guidelines 2026",
  "fileName": "Master_Brand_Guidelines_2026.pdf",
  "status": "Ready",
  "uploadedAt": "2026-10-27T00:46:41.258Z",
  "isActive": true,
  "chunkCount": 8,
  "textLength": 4120
}
```

### 1.2 Get All Guidelines
- **Endpoint:** `GET /api/guidelines`
- **Response (200 OK):** Array of `GuidelineDto`

### 1.3 Get Guideline by ID
- **Endpoint:** `GET /api/guidelines/{id}`
- **Response (200 OK):** `GuidelineDetailDto` with full chunks list

### 1.4 Get Chunks for Guideline
- **Endpoint:** `GET /api/guidelines/{id}/chunks`
- **Response (200 OK):** Array of `GuidelineChunkDto`

### 1.5 Delete Guideline
- **Endpoint:** `DELETE /api/guidelines/{id}`
- **Response (204 No Content)**

---

## 2. Content Submissions (`/api/content`)

### 2.1 Create Submission (Draft)
- **Endpoint:** `POST /api/content`
- **Body:**
```json
{
  "title": "Autumn Promotional Headline",
  "contentType": "Social Media Post",
  "contentText": "Our product guarantees 100% results across all deployments!"
}
```
- **Response (201 Created):** `ContentSubmissionDto`

### 2.2 List Submissions
- **Endpoint:** `GET /api/content`
- **Response (200 OK):** Array of `ContentSubmissionDto`

### 2.3 Get Submission by ID
- **Endpoint:** `GET /api/content/{id}`
- **Response (200 OK):** `ContentSubmissionDetailDto`

### 2.4 Update Submission
- **Endpoint:** `PUT /api/content/{id}`
- **Body:** `UpdateContentSubmissionDto`
- **Response (200 OK):** Updated `ContentSubmissionDto`

### 2.5 Trigger Hybrid Validation
- **Endpoint:** `POST /api/content/{id}/validate`
- **Description:** Triggers the end-to-end RAG pipeline, deterministic rule checks, and LLM synthesis.
- **Response (200 OK):** `ValidationResultDto`
```json
{
  "validationRunId": "f0a8ed46-be1d-450a-b994-0521971dbfbe",
  "contentSubmissionId": "d0c8022f-1fa7-4f12-9f1a-7b4a4d182562",
  "submissionTitle": "Spring Product Launch",
  "contentType": "Social Media Post",
  "originalContent": "Our product guarantees 100% results and is the best in the world.",
  "status": "Completed",
  "overallRiskLevel": "High",
  "totalFindings": 3,
  "highRiskCount": 2,
  "mediumRiskCount": 1,
  "lowRiskCount": 0,
  "startedAt": "2026-10-27T00:46:41.433Z",
  "completedAt": "2026-10-27T00:46:41.455Z",
  "findings": [
    {
      "id": "6b02c7d8-4219-4bbe-814c-5322f9656141",
      "validationRunId": "f0a8ed46-be1d-450a-b994-0521971dbfbe",
      "guidelineChunkId": "e53248c0-287e-4f46-95ce-72e45b26b248",
      "issueType": "No Absolute Guarantees",
      "severity": "High",
      "originalText": "100% results",
      "explanation": "The phrase '100% results' represents an absolute guarantee. Brand policy strictly prohibits promise of 100% or guaranteed outcomes.",
      "suggestedFix": "Replace absolute promises with measurable qualifications.",
      "evidenceText": "Policy 2.1: Absolute Guarantees Prohibited. Marketing content must not contain absolute outcome guarantees...",
      "sourceGuidelineTitle": "BrandGuard Master Brand Guidelines 2026",
      "pageNumber": 1,
      "sectionTitle": "Page 1",
      "createdAt": "2026-10-27T00:46:41.433Z"
    }
  ]
}
```

### 2.6 Get Latest Validation Result
- **Endpoint:** `GET /api/content/{id}/validation-result`
- **Response (200 OK):** `ValidationResultDto`

---

## 3. Human Governance & Reviews (`/api/reviews`)

### 3.1 Submit Review Decision
- **Endpoint:** `POST /api/reviews`
- **Body:**
```json
{
  "contentSubmissionId": "d0c8022f-1fa7-4f12-9f1a-7b4a4d182562",
  "validationRunId": "f0a8ed46-be1d-450a-b994-0521971dbfbe",
  "reviewerName": "Atharv Khot (Lead Reviewer)",
  "decision": "ChangesRequested",
  "comments": "Resolved after addressing absolute guarantee in accordance with Policy 2.1."
}
```
- **Response (200 OK):** `ReviewDecisionDto`

### 3.2 Get Review Audit History
- **Endpoint:** `GET /api/reviews/history`
- **Response (200 OK):** Array of `ReviewHistoryItemDto`

### 3.3 Get Decision for Submission
- **Endpoint:** `GET /api/reviews/submission/{submissionId}`
- **Response (200 OK):** `ReviewDecisionDto`

---

## 4. Dashboard Metrics (`/api/dashboard`)

### 4.1 Get Dashboard Statistics
- **Endpoint:** `GET /api/dashboard/stats`
- **Response (200 OK):**
```json
{
  "totalGuidelines": 1,
  "totalContentSubmissions": 2,
  "pendingReviews": 1,
  "approvedSubmissions": 0,
  "rejectedSubmissions": 0,
  "changesRequestedSubmissions": 1,
  "highRiskFindings": 2,
  "mediumRiskFindings": 2,
  "lowRiskFindings": 0,
  "recentSubmissions": [...]
}
```

---

## 5. Judge Demonstration Endpoints (`/api/demo`)

### 5.1 One-Click Seed Environment
- **Endpoint:** `POST /api/demo/seed`
- **Description:** Seeds guidelines, sample submissions, and executes validation run.

### 5.2 Download Demonstration Guideline PDF
- **Endpoint:** `GET /api/demo/sample-pdf`
- **Response (200 OK):** `application/pdf` binary stream.

### 5.3 Retrieve Pre-Set Sample Prompts
- **Endpoint:** `GET /api/demo/samples`
- **Response (200 OK):** JSON containing compliant and non-compliant samples.
