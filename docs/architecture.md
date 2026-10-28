# BrandGuard AI &mdash; System Architecture & Technical Design

## 1. High-Level Architecture Overview

BrandGuard AI is structured following Clean Architecture principles, ensuring modular separation of concerns between API endpoints, business logic, persistence, and external integrations.

```mermaid
graph TD
    Client["Angular 21 Client (Standalone Components & Signals)"] -->|REST API over HTTP/HTTPS| API["BrandGuardAI.API (ASP.NET Core Controllers)"]
    API --> App["BrandGuardAI.Application (Services, DTOs, Interfaces)"]
    App --> Domain["BrandGuardAI.Domain (Entities, Enums, Value Objects)"]
    Infra["BrandGuardAI.Infrastructure (EF Core, PDF, AI/RAG)"] --> App
    Infra --> Domain
    Infra --> DB[("Database: SQL Server / SQLite Fallback")]
    Infra --> LLM["External AI Provider (OpenRouter / OpenAI)"]
```

---

## 2. Solution Structure

```
BrandGuardAI/
├── BrandGuardAI.API/               # REST API Controllers, Middleware, CORS, Swagger
├── BrandGuardAI.Application/       # Core business interfaces, models, and service contracts
├── BrandGuardAI.Domain/            # Pure domain entities, enums, and lifecycle definitions
├── BrandGuardAI.Infrastructure/    # EF Core, PDF extraction, vector embeddings, RAG, and AI integration
├── BrandGuardAI.Tests/             # Unit and integration test suites
├── brandguard-ui/                  # Angular standalone frontend application
├── docs/                           # Architecture, guides, testing reports, and progress documents
├── samples/                        # Demonstration guideline PDFs and sample test files
└── README.md
```

---

## 3. The Retrieval-Augmented Generation (RAG) Pipeline

The RAG workflow transforms unstructured brand policy documents into structured, cited compliance findings:

```mermaid
sequenceDiagram
    actor Marketer as Marketing Team
    participant UI as Angular UI
    participant API as API Controller
    participant Orch as Validation Orchestrator
    participant RAG as Semantic Search & Chunker
    participant Rules as Deterministic Engine
    participant LLM as LLM Validation Service
    participant DB as SQL Server / SQLite

    Note over Marketer,DB: Ingestion Phase (Brand Guidelines)
    Marketer->>UI: Upload Brand Guideline PDF
    UI->>API: POST /api/guidelines/upload
    API->>RAG: Extract text page-by-page & chunk document
    RAG->>RAG: Generate 256-dim normalized vector embeddings
    RAG->>DB: Store Guideline & GuidelineChunks with vector JSON
    API-->>UI: Guideline Ready (status: Ready)

    Note over Marketer,DB: Validation Phase (Draft Copy)
    Marketer->>UI: Submit marketing copy
    UI->>API: POST /api/content/{id}/validate
    API->>Orch: RunValidationAsync(submissionId)
    Orch->>Rules: Match deterministic rules (guarantees, superlatives, disclaimers)
    Orch->>RAG: Cosine similarity vector search (top-5 chunks)
    Orch->>LLM: Send content + retrieved chunks + deterministic findings
    LLM-->>Orch: Structured JSON findings with citation candidate IDs
    Orch->>Orch: Verify citations (revoke un-retrieved chunk IDs)
    Orch->>Orch: Merge findings, deduplicate, calculate overall risk
    Orch->>DB: Persist ValidationRun & ValidationFindings
    Orch-->>UI: ValidationResultDto (High/Medium/Low, findings with citations)

    Note over Marketer,DB: Human Governance Phase
    actor Reviewer as Human Reviewer
    Reviewer->>UI: Inspect findings (Standard / Focus / Batch)
    Reviewer->>UI: Submit decision (Approve / Request Changes / Reject)
    UI->>API: POST /api/reviews
    API->>DB: Save ReviewDecision & update ContentSubmission status
    API-->>UI: Decision recorded in audit log
```

---

## 4. Entity Relationship Diagram

```mermaid
erDiagram
    BrandGuidelines ||--o{ GuidelineChunks : "has chunks"
    BrandGuidelines ||--o{ ValidationRules : "originates rules"
    ContentSubmissions ||--o{ ValidationRuns : "evaluated in"
    ContentSubmissions ||--o{ ReviewDecisions : "adjudicated by"
    ValidationRuns ||--o{ ValidationFindings : "produces findings"
    ValidationRuns ||--o{ ReviewDecisions : "reviewed in"
    GuidelineChunks ||--o{ ValidationFindings : "cited in"

    BrandGuidelines {
        Guid Id PK
        string Title
        string FileName
        string FilePath
        string ExtractedText
        string Status
        DateTime UploadedAt
        bool IsActive
    }

    GuidelineChunks {
        Guid Id PK
        Guid BrandGuidelineId FK
        int ChunkIndex
        string ChunkText
        int PageNumber
        string SectionTitle
        string EmbeddingData
        DateTime CreatedAt
    }

    ContentSubmissions {
        Guid Id PK
        string Title
        string ContentType
        string ContentText
        string Status
        DateTime CreatedAt
        DateTime UpdatedAt
    }

    ValidationRuns {
        Guid Id PK
        Guid ContentSubmissionId FK
        string Status
        DateTime StartedAt
        DateTime CompletedAt
        string OverallRiskLevel
        int TotalFindings
        string ErrorMessage
    }

    ValidationFindings {
        Guid Id PK
        Guid ValidationRunId FK
        Guid GuidelineChunkId FK
        string IssueType
        string Severity
        string OriginalText
        string Explanation
        string SuggestedFix
        string EvidenceText
        DateTime CreatedAt
    }

    ReviewDecisions {
        Guid Id PK
        Guid ContentSubmissionId FK
        Guid ValidationRunId FK
        string ReviewerName
        string Decision
        string Comments
        DateTime ReviewedAt
    }

    ValidationRules {
        Guid Id PK
        string RuleName
        string RuleType
        string RuleValue
        string Severity
        string Description
        Guid SourceGuidelineId FK
        bool IsActive
    }
```

---

## 5. Security & Reliability Highlights

1. **Parameterization:** All database operations utilize EF Core parameterized queries, preventing SQL injection.
2. **File Validation:** PDF uploads are restricted to `.pdf` extension, validated for binary stream integrity, and capped at 25MB.
3. **Evidence Anti-Hallucination:** Every chunk cited in LLM output is programmatically verified against the in-memory dictionary of retrieved chunks. If an ID was fabricated, the citation is revoked to prevent phantom evidence.
4. **Idempotency Protection:** Consecutive duplicate review decisions submitted within 15 seconds are intercepted, returning the existing decision without creating redundant database records.
5. **Database Resilience:** Automated fallback between SQL Server and SQLite ensures zero environmental startup failures.
