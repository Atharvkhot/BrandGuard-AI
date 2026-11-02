import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import {
  ValidationResultDto,
  FindingDto,
  ReviewDecisionType,
  ReviewDecisionDto,
  SubmitReviewDecisionDto,
  ContentSubmissionDto,
  BatchReviewDecisionDto,
  BatchReviewResultDto
} from '../../models/brandguard.models';

export type ViewMode = 'standard' | 'guided' | 'batch';
export type BatchSubTab = 'findings' | 'submissions';

@Component({
  selector: 'app-validation-results',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './validation-results.component.html',
  styleUrls: ['./validation-results.component.scss']
})
export class ValidationResultsComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private api = inject(ApiService);

  submissionId: string = '';
  validationResult = signal<ValidationResultDto | null>(null);
  loading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  // Existing Review Decision on this submission
  existingDecision = signal<ReviewDecisionDto | null>(null);

  // View Mode: 'standard' | 'guided' | 'batch'
  activeMode = signal<ViewMode>('standard');

  // ================= GUIDED FOCUS MODE STATE =================
  currentFocusIndex = signal<number>(0);
  guidedReviewerName: string = 'Judge Reviewer';
  guidedDecision: ReviewDecisionType = 'ChangesRequested';
  guidedComments: string = '';
  submittingGuidedDecision = signal<boolean>(false);
  guidedSuccessMessage = signal<string | null>(null);
  guidedErrorMessage = signal<string | null>(null);

  decisionOptions: { type: ReviewDecisionType; label: string; icon: string; cssClass: string; desc: string }[] = [
    {
      type: 'Approved',
      label: 'Approve Content',
      icon: 'bi-check-circle-fill',
      cssClass: 'btn-outline-success',
      desc: 'Content complies with brand policy and is approved for publication.'
    },
    {
      type: 'ChangesRequested',
      label: 'Request Changes',
      icon: 'bi-pencil-square',
      cssClass: 'btn-outline-warning',
      desc: 'Content requires revision to address flagged brand guideline findings.'
    },
    {
      type: 'Rejected',
      label: 'Reject Content',
      icon: 'bi-x-circle-fill',
      cssClass: 'btn-outline-danger',
      desc: 'Content violates fundamental corporate policies and cannot be distributed.'
    }
  ];

  // ================= QUICK BATCH MODE STATE =================
  batchSubTab = signal<BatchSubTab>('findings');

  // Finding Acknowledgment
  selectedFindingIds = new Set<string>();
  acknowledgingFindings = signal<boolean>(false);
  batchMessage = signal<{ type: 'success' | 'error'; text: string } | null>(null);

  // Submissions Batch Review
  allSubmissions = signal<ContentSubmissionDto[]>([]);
  loadingSubmissions = signal<boolean>(false);
  selectedSubmissionIds = new Set<string>();
  batchSubmissionDecision: ReviewDecisionType = 'Approved';
  batchSubmissionReviewer: string = 'Judge Reviewer';
  batchSubmissionComments: string = '';
  confirmHighRiskApproval: boolean = false;
  showBatchConfirmModal = signal<boolean>(false);
  submittingBatchReview = signal<boolean>(false);
  batchReviewResult = signal<BatchReviewResultDto | null>(null);
  batchReviewError = signal<string | null>(null);

  // Evidence Preview Modal State
  selectedEvidenceFinding = signal<FindingDto | null>(null);

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      this.submissionId = params['id'];
      if (this.submissionId) {
        this.loadValidationResults();
        this.loadExistingDecision();
      }
    });
  }

  loadValidationResults(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.api.getValidationResult(this.submissionId).subscribe({
      next: (result) => {
        this.validationResult.set(result);
        this.setDefaultComments(result);
        this.loading.set(false);
      },
      error: () => {
        this.api.validateContent(this.submissionId).subscribe({
          next: (run) => {
            this.validationResult.set(run);
            this.setDefaultComments(run);
            this.loading.set(false);
          },
          error: () => {
            this.errorMessage.set('Unable to retrieve or execute validation results for this submission.');
            this.loading.set(false);
          }
        });
      }
    });
  }

  loadExistingDecision(): void {
    this.api.getDecisionBySubmissionId(this.submissionId).subscribe({
      next: (decision) => {
        if (decision) {
          this.existingDecision.set(decision);
          this.guidedReviewerName = decision.reviewerName || this.guidedReviewerName;
          this.guidedDecision = decision.decision || this.guidedDecision;
          this.guidedComments = decision.comments || this.guidedComments;
        }
      },
      error: () => {}
    });
  }

  setDefaultComments(res: ValidationResultDto): void {
    if (res.overallRiskLevel === 'High') {
      this.guidedDecision = 'ChangesRequested';
      this.guidedComments = 'Please resolve flagged high-risk claims before publication.';
    } else if (res.overallRiskLevel === 'Low') {
      this.guidedDecision = 'Approved';
      this.guidedComments = 'Verified compliance against brand guidelines.';
    } else {
      this.guidedDecision = 'ChangesRequested';
      this.guidedComments = 'Moderate risk issues flagged; revision suggested.';
    }
  }

  setMode(mode: ViewMode): void {
    this.activeMode.set(mode);
    this.currentFocusIndex.set(0);
    this.batchMessage.set(null);
    this.guidedSuccessMessage.set(null);
    this.guidedErrorMessage.set(null);

    if (mode === 'batch' && this.batchSubTab() === 'submissions') {
      this.loadAllSubmissions();
    }
  }

  setBatchSubTab(tab: BatchSubTab): void {
    this.batchSubTab.set(tab);
    this.batchMessage.set(null);
    this.batchReviewError.set(null);
    this.batchReviewResult.set(null);

    if (tab === 'submissions' && this.allSubmissions().length === 0) {
      this.loadAllSubmissions();
    }
  }

  // ================= GUIDED FOCUS MODE NAVIGATION & DECISION =================
  previousFocus(): void {
    if (this.currentFocusIndex() > 0) {
      this.currentFocusIndex.update(i => i - 1);
    }
  }

  nextFocus(): void {
    const total = this.validationResult()?.findings.length ?? 0;
    if (this.currentFocusIndex() < total - 1) {
      this.currentFocusIndex.update(i => i + 1);
    }
  }

  getCurrentFocusFinding(): FindingDto | null {
    const findings = this.validationResult()?.findings ?? [];
    if (findings.length === 0) return null;
    return findings[this.currentFocusIndex()] ?? null;
  }

  submitGuidedDecision(): void {
    if (!this.guidedReviewerName.trim()) {
      this.guidedErrorMessage.set('Reviewer name is required for governance auditability.');
      return;
    }

    if (this.submittingGuidedDecision()) return;

    this.submittingGuidedDecision.set(true);
    this.guidedSuccessMessage.set(null);
    this.guidedErrorMessage.set(null);

    const dto: SubmitReviewDecisionDto = {
      contentSubmissionId: this.submissionId,
      validationRunId: this.validationResult()?.validationRunId,
      reviewerName: this.guidedReviewerName.trim(),
      decision: this.guidedDecision,
      comments: this.guidedComments.trim()
    };

    this.api.submitReviewDecision(dto).subscribe({
      next: (result) => {
        this.submittingGuidedDecision.set(false);
        this.existingDecision.set(result);
        this.guidedSuccessMessage.set(
          `Decision successfully recorded: ${result.decision} by ${result.reviewerName}. Status updated in database.`
        );

        // Update local submission status without page reload
        const currentVal = this.validationResult();
        if (currentVal) {
          currentVal.status = result.decision;
          this.validationResult.set({ ...currentVal });
        }
      },
      error: (err) => {
        this.submittingGuidedDecision.set(false);
        this.guidedErrorMessage.set(err.error?.error || 'Failed to record human review decision.');
      }
    });
  }

  // ================= QUICK BATCH MODE: FINDING ACKNOWLEDGMENT =================
  toggleSelection(findingId: string): void {
    if (this.selectedFindingIds.has(findingId)) {
      this.selectedFindingIds.delete(findingId);
    } else {
      this.selectedFindingIds.add(findingId);
    }
  }

  selectAll(event: any): void {
    const checked = event.target.checked;
    const findings = this.validationResult()?.findings ?? [];
    if (checked) {
      findings.forEach(f => this.selectedFindingIds.add(f.id));
    } else {
      this.selectedFindingIds.clear();
    }
  }

  isSelected(findingId: string): boolean {
    return this.selectedFindingIds.has(findingId);
  }

  isFindingAcknowledged(finding: FindingDto): boolean {
    return !!(finding.isAcknowledged || finding.acknowledged);
  }

  acknowledgeSelected(): void {
    if (this.selectedFindingIds.size === 0 || this.acknowledgingFindings()) return;

    const findingIds = Array.from(this.selectedFindingIds);
    this.acknowledgingFindings.set(true);
    this.batchMessage.set(null);

    this.api.acknowledgeFindings({
      findingIds,
      acknowledgedBy: this.guidedReviewerName.trim() || 'Reviewer'
    }).subscribe({
      next: (res) => {
        const val = this.validationResult();
        if (val) {
          val.findings.forEach(f => {
            if (this.selectedFindingIds.has(f.id)) {
              f.isAcknowledged = true;
              f.acknowledged = true;
              f.acknowledgedAt = new Date().toISOString();
              f.acknowledgedBy = this.guidedReviewerName.trim() || 'Reviewer';
            }
          });
          this.validationResult.set({ ...val });
        }

        this.batchMessage.set({
          type: 'success',
          text: `Successfully acknowledged and persisted ${res.acknowledgedCount} findings in database.`
        });
        this.selectedFindingIds.clear();
        this.acknowledgingFindings.set(false);
      },
      error: (err) => {
        this.batchMessage.set({
          type: 'error',
          text: err.error?.error || 'Failed to persist finding acknowledgments in database.'
        });
        this.acknowledgingFindings.set(false);
      }
    });
  }

  // ================= QUICK BATCH MODE: MULTI-SUBMISSION BATCH REVIEW =================
  loadAllSubmissions(): void {
    this.loadingSubmissions.set(true);
    this.api.getSubmissions().subscribe({
      next: (subs) => {
        this.allSubmissions.set(subs);
        this.loadingSubmissions.set(false);
      },
      error: () => {
        this.loadingSubmissions.set(false);
      }
    });
  }

  toggleSubmissionSelection(subId: string): void {
    if (this.selectedSubmissionIds.has(subId)) {
      this.selectedSubmissionIds.delete(subId);
    } else {
      this.selectedSubmissionIds.add(subId);
    }
  }

  selectAllSubmissions(event: any): void {
    const checked = event.target.checked;
    if (checked) {
      this.allSubmissions().forEach(s => this.selectedSubmissionIds.add(s.id));
    } else {
      this.selectedSubmissionIds.clear();
    }
  }

  isSubmissionSelected(subId: string): boolean {
    return this.selectedSubmissionIds.has(subId);
  }

  hasSelectedHighRiskSubmission(): boolean {
    const subs = this.allSubmissions().filter(s => this.selectedSubmissionIds.has(s.id));
    return subs.some(s => s.latestRiskLevel === 'High');
  }

  getSelectedHighRiskCount(): number {
    const subs = this.allSubmissions().filter(s => this.selectedSubmissionIds.has(s.id));
    return subs.filter(s => s.latestRiskLevel === 'High').length;
  }

  initiateBatchReview(): void {
    if (this.selectedSubmissionIds.size === 0) return;

    if (!this.batchSubmissionReviewer.trim()) {
      this.batchReviewError.set('Reviewer name is required for batch governance auditability.');
      return;
    }

    // Safety guard: prevent accidental approval of high-risk content
    if (this.batchSubmissionDecision === 'Approved' && this.hasSelectedHighRiskSubmission() && !this.confirmHighRiskApproval) {
      this.batchReviewError.set(
        `Accidental High-Risk Approval Prevention: ${this.getSelectedHighRiskCount()} selected submission(s) contain High Risk findings. You must explicitly confirm the override checkbox below before approving.`
      );
      return;
    }

    this.batchReviewError.set(null);
    this.showBatchConfirmModal.set(true);
  }

  closeBatchConfirmModal(): void {
    this.showBatchConfirmModal.set(false);
  }

  confirmAndApplyBatchReview(): void {
    this.showBatchConfirmModal.set(false);
    this.submittingBatchReview.set(true);
    this.batchReviewError.set(null);
    this.batchReviewResult.set(null);

    const dto: BatchReviewDecisionDto = {
      contentSubmissionIds: Array.from(this.selectedSubmissionIds),
      decision: this.batchSubmissionDecision,
      reviewerName: this.batchSubmissionReviewer.trim(),
      comments: this.batchSubmissionComments.trim(),
      confirmHighRiskApproval: this.confirmHighRiskApproval
    };

    this.api.submitBatchReview(dto).subscribe({
      next: (res) => {
        this.batchReviewResult.set(res);
        this.submittingBatchReview.set(false);
        this.selectedSubmissionIds.clear();
        this.confirmHighRiskApproval = false;

        // Reload submissions list
        this.loadAllSubmissions();

        // If current submission was updated, refresh its decision
        if (dto.contentSubmissionIds.includes(this.submissionId)) {
          this.loadExistingDecision();
          const current = this.validationResult();
          if (current) {
            current.status = dto.decision;
            this.validationResult.set({ ...current });
          }
        }
      },
      error: (err) => {
        this.batchReviewError.set(err.error?.error || 'Failed to execute batch review.');
        this.submittingBatchReview.set(false);
      }
    });
  }

  // ================= EVIDENCE PREVIEW MODAL =================
  openEvidenceModal(finding: FindingDto): void {
    this.selectedEvidenceFinding.set(finding);
  }

  closeEvidenceModal(): void {
    this.selectedEvidenceFinding.set(null);
  }

  hasValidEvidence(finding?: FindingDto | null): boolean {
    if (!finding) return false;
    return !!(finding.hasVerifiedEvidence && finding.evidenceText && finding.evidenceText.trim().length > 0);
  }

  getProvenanceLabel(provenance?: string): string {
    switch (provenance) {
      case 'DirectGuidelineMatch': return 'Direct Guideline Match';
      case 'DeterministicRule': return 'Deterministic Rule';
      case 'UnverifiedAIClaim': return 'Unverified AI Claim';
      case 'NoEvidence': return 'No Verified Evidence';
      default: return 'General Policy';
    }
  }

  getProvenanceBadgeClass(provenance?: string): string {
    switch (provenance) {
      case 'DirectGuidelineMatch': return 'badge-provenance-match';
      case 'DeterministicRule': return 'badge-provenance-deterministic';
      case 'UnverifiedAIClaim': return 'badge-provenance-unverified';
      case 'NoEvidence': return 'badge-provenance-none';
      default: return 'badge-provenance-none';
    }
  }

  getProvenanceIcon(provenance?: string): string {
    switch (provenance) {
      case 'DirectGuidelineMatch': return 'bi-shield-check';
      case 'DeterministicRule': return 'bi-gear-wide-connected';
      case 'UnverifiedAIClaim': return 'bi-robot';
      case 'NoEvidence': return 'bi-question-circle';
      default: return 'bi-info-circle';
    }
  }

  goToReview(): void {
    this.router.navigate(['/review', this.submissionId]);
  }
}
