# BrandGuard AI &mdash; Enterprise Marketing Governance Platform

> **"AI Detects. Evidence Explains. Humans Decide."**

**Hackathon:** Codex 48 &bull; Round 2  
**Team Name:** TechDuo  
**Team Leader:** Atharv Raghunath Khot  
**Team Member:** Ishwari Ganesh Kadam  

---

## 1. Product Overview

**BrandGuard AI** is an evidence-first brand governance and marketing content compliance validation platform. Organizations upload approved brand guidelines, corporate policies, terminology rules, and regulatory directives. When marketing teams submit draft copy, the platform evaluates the material against the indexed knowledge base using **Retrieval-Augmented Generation (RAG)** combined with **deterministic rule validation**.

Every compliance finding is backed by verifiable quotes and page numbers from official guidelines, calculating risk severity (High, Medium, Low) and presenting actionable corrections. Crucially, AI informs rather than dictates: human reviewers review findings in a dedicated interface (with standard, Guided Focus, and Quick Batch modes) to make final binding decisions that are immutably logged for auditability.

---

## 2. Key Features

- **Evidence-First Retrieval-Augmented Generation (RAG):** Real vector embeddings and cosine similarity search over chunked PDF guidelines.
- **Strict Evidence Citation Verification:** The AI engine is prohibited from citing non-retrieved or hallucinated documents. Every citation is programmatically validated against retrieved chunks.
- **Hybrid Compliance Engine:**
  - *Deterministic Validation:* Immediate detection of absolute outcome guarantees ("guarantees 100% results"), prohibited terminology, superlative leadership claims ("best in the world"), and missing mandatory disclaimers.
  - *LLM Contextual Validation:* Deep brand voice analysis, tone consistency, and contextual conflict resolution.
- **Three Review Experiences:**
  1. *Standard Full View:* Comprehensive catalog of all flagged issues with evidence and suggested fixes.
  2. *Guided Focus Mode:* Step-by-step review interface inspecting one finding at a time without cognitive clutter.
  3. *Quick Batch Mode:* Multi-select batch review to acknowledge findings and expedite sign-off.
- **Persistent Human Governance:** Review decisions (Approve, Request Changes, Reject) with reviewer identity, editorial comments, and timestamps persisted to database.
- **Judge-Ready One-Click Demonstration:** Built-in seed data loader and sample guideline PDF generation for instant evaluation.

---

## 3. Technology Stack

### Frontend
- **Framework:** Angular (Standalone Components, Signals, Reactive Forms, Router)
- **Styling:** Bootstrap 5, Bootstrap Icons, Custom SCSS Enterprise Design System
- **HTTP & State:** HttpClient with Fetch, RxJS Observables, Angular Signals

### Backend
- **Framework:** ASP.NET Core Web API (.NET 9)
- **Language:** C#
- **Data Access:** Entity Framework Core (Code First & Migrations)
- **PDF Extraction:** UglyToad.PdfPig (Page-by-page text & layout parsing)
- **API Documentation:** Swagger / OpenAPI

### Database & Storage
- **Primary:** Microsoft SQL Server (EF Core Code First)
- **Resilience Fallback:** SQLite (`Data Source=BrandGuardAI.db`) for seamless standalone evaluation without local SQL Server runtime dependencies.

### AI & Embeddings
- **LLM Provider:** Configurable via OpenRouter (`google/gemini-2.5-flash`), OpenAI, or Anthropic.
- **Embeddings:** Dense 256-dimensional normalized vectorizer with term, bigram, and stem weighting, interoperable with external embedding APIs.

---

## 4. Quick Start & Execution

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js (v18+ or v20+)](https://nodejs.org/) & npm

### Backend Setup & Launch
```bash
# From workspace root:
dotnet build BrandGuardAI.sln
dotnet run --project BrandGuardAI.API --launch-profile http
```
The API starts at `http://localhost:5000` (Swagger UI at `http://localhost:5000/swagger`).

### Frontend Setup & Launch
```bash
# In a separate terminal:
cd brandguard-ui
npm install
npm start
```
Open your browser at `http://localhost:4200`.

---

## 5. Judge Demonstration Scenario

1. Open `http://localhost:4200` to access the BrandGuard AI dashboard.
2. Click **"Quick Seed Demo Data"** on the dashboard (or upload `samples/BrandGuard_Master_Guidelines_2026.pdf` on the **Brand Guidelines** page).
3. Under **Submit Content**, click **"Load Violating Sample"** (injects: *"Our product guarantees 100% results and is the best solution in the world."*).
4. Click **"Validate Content Now"**.
5. Observe the live validation results:
   - **High Risk** overall classification.
   - **Absolute Guarantee finding:** Flags *"100% results"* citing Policy 2.1 on Page 1.
   - **Superlative Claim finding:** Flags *"best in the world"* citing Policy 2.2 on Page 1.
   - **Missing Disclaimer finding:** Recommends standard terms disclosure.
6. Test **Guided Focus Mode** to step through each finding one-by-one.
7. Click **"Proceed to Human Review"**, enter reviewer name, select **"Request Changes"**, provide comments, and click **"Save & Record Decision"**.
8. Navigate to **Review History** to view the recorded audit entry.
9. Refresh the page to verify persistent database storage.

---

## 6. Automated Testing

Run the test suite across unit and integration scenarios:
```bash
dotnet test BrandGuardAI.Tests/BrandGuardAI.Tests.csproj
```
**Results:** 10 Passed, 0 Failed, 0 Skipped (100% pass rate).

---

## 7. Project Documentation Index

- [Architecture Design](file:///d:/New%20folder%20(2)/BrandGuard%20AI/docs/architecture.md)
- [API Reference](file:///d:/New%20folder%20(2)/BrandGuard%20AI/docs/api-reference.md)
- [Setup & Deployment Guide](file:///d:/New%20folder%20(2)/BrandGuard%20AI/docs/setup-guide.md)
- [Automated Testing Report](file:///d:/New%20folder%20(2)/BrandGuard%20AI/docs/testing-report.md)
- [Round 1 to Round 2 Progress Matrix](file:///d:/New%20folder%20(2)/BrandGuard%20AI/docs/round1-to-round2-progress.md)
- [Known Limitations](file:///d:/New%20folder%20(2)/BrandGuard%20AI/docs/known-limitations.md)
- [Round 3 Major Improvements](file:///d:/New%20folder%20(2)/BrandGuard%20AI/docs/round3-improvements.md)
- [Submission Checklist](file:///d:/New%20folder%20(2)/BrandGuard%20AI/docs/submission-checklist.md)
