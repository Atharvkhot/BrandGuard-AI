# BrandGuard AI &mdash; Known Limitations & Trade-Offs

In accordance with Hackathon Criterion 2 (*Technical Depth and Reliability*) and Section 26 of the master guidelines, this document transparently details the known technical limitations and intentional design trade-offs of the Round 2 MVP.

---

## 1. Authentication & Authorization
- **Current State:** Reviewer identity is established via a required `ReviewerName` field recorded with each decision.
- **Limitation:** The Round 2 MVP does not implement an enterprise Single Sign-On (SSO), OAuth2, or SAML authentication provider.
- **Rationale:** Prioritized the end-to-end RAG validation pipeline, deterministic rules, and audit persistence over login screens, allowing judges immediate evaluation access without credentials.
- **Round 3 Plan:** Implement ASP.NET Core Identity with Microsoft Entra ID (Azure AD) and role-based permissions (Reviewer, Admin, Marketer).

---

## 2. In-Process Vector Indexing vs. Distributed Vector DB
- **Current State:** Guideline chunk embeddings are stored as JSON float arrays in the database and loaded for cosine similarity matching during validation.
- **Limitation:** In-process vector calculation scales efficiently for corporate guideline libraries of hundreds of pages (typically 50–500 chunks), but is not suited for millions of documents.
- **Rationale:** Eliminates external vector database dependencies (e.g., Pinecone, Milvus), ensuring the MVP can be cloned and run instantly by any judge on any machine without Docker or cloud vector clusters.
- **Round 3 Plan:** Introduce pgvector or Qdrant for enterprise-scale multi-tenant vector storage.

---

## 3. PDF Parsing Scope (Text Streams vs. Scanned Images)
- **Current State:** PDF extraction is handled by `UglyToad.PdfPig`, which reads digital text streams, font structures, and page boundaries.
- **Limitation:** Scanned raster image PDFs (flat images without embedded text layers) yield empty text streams unless pre-processed with Optical Character Recognition (OCR).
- **Rationale:** 95%+ of modern enterprise brand guidelines are exported as digital vector PDFs from Figma, Adobe InDesign, or Microsoft Word.
- **Round 3 Plan:** Add Tesseract OCR / Azure AI Vision fallback for scanned heritage guidelines.

---

## 4. Single-Language Focus
- **Current State:** Deterministic rules and prompt templates are optimized for English brand guidelines and marketing copy.
- **Limitation:** Non-English marketing copy will trigger semantic embedding matching, but deterministic regex clauses (e.g., "100% results", "guarantee") require English tokenization.
- **Round 3 Plan:** Implement multi-lingual tokenization and localized rule registers.
