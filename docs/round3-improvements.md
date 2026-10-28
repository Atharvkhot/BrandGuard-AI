# BRANDGUARD AI — THREE ROUND 3 COMMITMENTS & ROADMAP
**Codex 48 Hackathon — Round 2 to Round 3 Proposal**
**Team:** TechDuo (Atharv Raghunath Khot & Ishwari Ganesh Kadam)
**Product:** BrandGuard AI
**Tagline:** *"AI Detects. Evidence Explains. Humans Decide."*

The following three substantial enhancements represent our technical commitments for **Round 3**. They build directly upon the foundation established in the Round 2 working MVP.

> [!IMPORTANT]
> **Honesty & Integrity Note:** These improvements are planned commitments for Round 3 and are not claimed as already completed in Round 2.

---

## IMPROVEMENT 1 — MULTIMODAL VALIDATION FOR MARKETING IMAGES AND BANNERS

### 1. User Problem
Enterprise marketing campaigns are rarely plain text. They consist of digital display ads, Instagram carousels, YouTube thumbnails, and promotional banners where non-compliant claims (e.g., *"100% Guaranteed!"*, *"Voted #1 Solution"*) are baked directly into raster images, bypassing text-only filters and triggering costly regulatory fines.

### 2. Proposed Functionality
Extend the BrandGuard AI ingestion and validation engine to accept image assets (PNG, JPEG, WebP, SVG). The system will visually inspect graphics, extract on-image typography, verify brand color palette conformity against approved HEX codes, check logo clear-space rules, and cross-reference extracted visual claims against indexed brand guideline PDF vectors.

### 3. Technical Approach
* **Vision-LLM & OCR Integration:** Utilize vision-capable APIs (Gemini 2.5 Flash Vision / GPT-4o Vision) coupled with targeted OCR preprocessing to extract text coordinates and bounding boxes.
* **Color Space Analysis:** Extract dominant image color clusters using k-means color quantization and compute Delta-E ($\Delta E$) color distance against approved corporate brand palette palettes.
* **Unified Evidence Mapping:** Extracted visual claims are converted into 256-dim unit vectors and fed into the existing `SemanticSearchService` to link image text to the exact clause in the approved brand guideline PDF.

### 4. Expected Impact
Enables marketing and creative design teams to validate complete visual display ads and promotional creatives in a single automated step, preventing regulatory violations before ad spend is committed.

### 5. Acceptance Criteria
* [ ] System accepts PNG, JPEG, and WebP uploads up to 25MB via a new `/api/guidelines/validate-image` endpoint.
* [ ] Text embedded in promotional imagery is accurately extracted with >95% accuracy.
* [ ] Color distance analysis flags unapproved primary brand colors with $\Delta E > 5.0$.
* [ ] Findings from visual text are attributed to authentic PDF guideline chunk IDs with page numbers.

---

## IMPROVEMENT 2 — REVIEWER FEEDBACK-DRIVEN GOVERNANCE RECOMMENDATIONS

### 1. User Problem
Brand guidelines and compliance rules in large corporations are static documents that quickly become outdated. When human reviewers repeatedly approve or modify specific findings (e.g., consistently overriding a "Restricted Phrase" flag for seasonal promotions), the system does not learn, causing reviewer fatigue from repeated false positives.

### 2. Proposed Functionality
An autonomous policy learning loop that analyzes historical `ReviewDecisions`, reviewer comments, and override patterns to proactively generate recommendations for updating corporate brand policies and deterministic regex rules.

### 3. Technical Approach
* **Decision Analytics Aggregator:** A background job running on a recurring schedule that clusters review overrides by rule type and keyword tokens.
* **Policy Drift Detection:** Computes override ratios per rule:
  $$\text{OverrideRate}(r) = \frac{\text{Count}(\text{Decisions} == \text{Approved} \mid \text{Finding}(r))}{\text{TotalFindings}(r)}$$
  If $\text{OverrideRate}(r) > 0.40$ over a 30-day window, a rule refinement signal is emitted.
* **AI Rule Synthesizer:** An offline LLM synthesizes a recommended rule adjustment (e.g., narrowing a regex pattern to allow a specific seasonal exception or suggesting an addendum to the master guideline).
* **Admin Governance Portal:** Staged recommendations are surfaced in an Administrator Portal requiring explicit human compliance officer sign-off before modification.

### 4. Expected Impact
Transforms corporate brand governance from a rigid rulebook into an adaptive, continuously improving system, cutting false positive reviews by an estimated 35% within 60 days of enterprise deployment.

### 5. Acceptance Criteria
* [ ] Aggregator identifies rules with $>40\%$ human override rates over a test set of $\ge 50$ reviews.
* [ ] Generates proposed rule modifications formatted as valid regex patterns with explanatory rationales.
* [ ] Proposed updates are presented in a dedicated admin review screen with "Accept Modification" or "Dismiss" actions.
* [ ] No rule modifications are automatically committed to the database without explicit administrator approval.

---

## IMPROVEMENT 3 — WORDPRESS & HEADLESS CMS PUBLISHING INTEGRATION

### 1. User Problem
Content creators and copywriters work directly in Content Management Systems (CMS) like WordPress, Contentful, or Webflow. Having to switch out of their authoring environment to copy-paste text into a separate compliance tool introduces workflow friction, causing writers to skip compliance validation altogether.

### 2. Proposed Functionality
A native **BrandGuard AI WordPress Gutenberg Plugin** and headless webhook connector that embeds real-time compliance validation directly inside the post editor sidebar, preventing publication of non-compliant marketing articles without authorized sign-off.

### 3. Technical Approach
* **Gutenberg Editor Sidebar:** A React-based WordPress sidebar block built with `@wordpress/plugins` and `@wordpress/edit-post`.
* **Pre-Publish Gatekeeper:** Hooks into the WordPress `pre_post_update` and Gutenberg `dispatch('core/editor').savePost` actions.
* **REST API Webhook:** Sends post title, excerpt, and body content to `POST /api/content/validate`.
* **Inline Annotation:** Renders compliance findings, severity badges, and suggested fixes directly alongside post paragraphs.
* **Publishing Lock:** Disables the "Publish" button if unresolved "High Risk" findings exist, unless an editor with `brand_governance_admin` role provides an override token.

### 4. Expected Impact
Brings automated compliance validation directly to content creators at the point of creation, completely eliminating manual copy-pasting and ensuring zero non-compliant drafts reach public distribution.

### 5. Acceptance Criteria
* [ ] WordPress plugin installs cleanly as a standard `.zip` package on WordPress 6.x.
* [ ] Gutenberg editor sidebar displays real-time compliance status fetched from the BrandGuard AI API.
* [ ] High-risk findings display one-click "Apply Suggested Fix" buttons that directly update the editor text block.
* [ ] Authors without governance approval are prevented from publishing posts marked with "High Risk" status.
