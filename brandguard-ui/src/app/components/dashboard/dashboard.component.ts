import { Component, OnInit, signal, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink, Router } from '@angular/router';
import { ApiService } from '../../services/api.service';
import {
  GuidelineDto,
  GuidelineDetailDto,
  ContentSubmissionDto,
  ValidationResultDto,
  FindingDto,
  ReviewDecisionType,
  SubmitReviewDecisionDto
} from '../../models/brandguard.models';

export type WorkspaceTab =
  | 'summary'
  | 'findings'
  | 'evidence'
  | 'risk'
  | 'compliance'
  | 'corrections'
  | 'review';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit {
  private api = inject(ApiService);
  private router = inject(Router);

  // Guidelines State
  guidelines = signal<GuidelineDto[]>([]);
  loadingGuidelines = signal<boolean>(true);

  // Submissions State
  submissions = signal<ContentSubmissionDto[]>([]);
  selectedSubmissionId = signal<string | null>(null);

  // Active Validation Result
  validationResult = signal<ValidationResultDto | null>(null);
  loadingValidation = signal<boolean>(false);

  // Analysis Workspace Navigation Tab
  activeTab = signal<WorkspaceTab>('summary');
  selectedFindingIndex = signal<number>(0);

  // Human Review Form
  reviewerName = 'QA Senior Reviewer';
  selectedDecision: ReviewDecisionType = 'ChangesRequested';
  reviewComments = 'Please address highlighted superlative and guarantee claims before syndication.';
  submittingReview = signal<boolean>(false);
  reviewMessage = signal<{ type: 'success' | 'error'; text: string } | null>(null);

  // Guideline Upload Modal
  showUploadModal = signal<boolean>(false);
  uploadFile: File | null = null;
  uploadTitle = '';
  isUploading = signal<boolean>(false);
  uploadError = signal<string | null>(null);
  uploadSuccess = signal<string | null>(null);

  // Compare Content / New Submission Modal
  showCompareModal = signal<boolean>(false);
  newTitle = 'Spring 2026 Social Campaign';
  newContentType = 'SocialMediaPost';
  newContentText = 'Our product guarantees 100% results and is the best solution in the world.';
  isValidatingNew = signal<boolean>(false);

  // Guideline Inspector Modal
  viewingGuideline = signal<GuidelineDetailDto | null>(null);

  // Clipboard Feedback
  copyFeedback = signal<string | null>(null);

  // Computed Properties
  wordCount = computed(() => {
    const text = this.newContentText.trim();
    return text ? text.split(/\s+/).length : 0;
  });

  charCount = computed(() => this.newContentText.length);

  selectedFinding = computed<FindingDto | null>(() => {
    const findings = this.validationResult()?.findings ?? [];
    if (findings.length === 0) return null;
    const idx = this.selectedFindingIndex();
    return findings[idx] ?? findings[0];
  });

  ngOnInit(): void {
    this.loadInitialData();
  }

  loadInitialData(): void {
    this.loadGuidelines();
    this.loadSubmissions();
  }

  loadGuidelines(): void {
    this.loadingGuidelines.set(true);
    this.api.getGuidelines().subscribe({
      next: (data) => {
        this.guidelines.set(data);
        this.loadingGuidelines.set(false);
      },
      error: (err) => {
        console.error('Failed to load guidelines', err);
        this.loadingGuidelines.set(false);
      }
    });
  }

  loadSubmissions(): void {
    this.api.getSubmissions().subscribe({
      next: (subs) => {
        this.submissions.set(subs);
        if (subs && subs.length > 0) {
          // Select first submission by default if none selected
          if (!this.selectedSubmissionId()) {
            this.selectSubmission(subs[0].id);
          }
        }
      },
      error: (err) => console.error('Failed to load submissions', err)
    });
  }

  selectSubmission(id: string): void {
    this.selectedSubmissionId.set(id);
    this.selectedFindingIndex.set(0);
    this.loadingValidation.set(true);

    this.api.getValidationResult(id).subscribe({
      next: (result) => {
        this.validationResult.set(result);
        this.loadingValidation.set(false);
      },
      error: () => {
        // Run validation if not already present
        this.api.validateContent(id).subscribe({
          next: (run) => {
            this.validationResult.set(run);
            this.loadingValidation.set(false);
          },
          error: (err) => {
            console.error('Validation failed for submission', id, err);
            this.validationResult.set(null);
            this.loadingValidation.set(false);
          }
        });
      }
    });
  }

  setTab(tab: WorkspaceTab): void {
    this.activeTab.set(tab);
  }

  selectFinding(index: number): void {
    this.selectedFindingIndex.set(index);
  }

  copyCorrection(text: string): void {
    navigator.clipboard.writeText(text).then(() => {
      this.copyFeedback.set('Correction copied to clipboard!');
      setTimeout(() => this.copyFeedback.set(null), 3000);
    });
  }

  // Submit Human Review Decision
  submitReview(): void {
    const subId = this.selectedSubmissionId();
    if (!subId || !this.reviewerName.trim()) return;

    this.submittingReview.set(true);
    this.reviewMessage.set(null);

    const dto: SubmitReviewDecisionDto = {
      contentSubmissionId: subId,
      validationRunId: this.validationResult()?.validationRunId,
      reviewerName: this.reviewerName.trim(),
      decision: this.selectedDecision,
      comments: this.reviewComments.trim()
    };

    this.api.submitReviewDecision(dto).subscribe({
      next: (res) => {
        this.submittingReview.set(false);
        this.reviewMessage.set({
          type: 'success',
          text: `Review decision "${res.decision}" recorded by ${res.reviewerName}. Status updated in database.`
        });
        this.loadSubmissions();
        setTimeout(() => this.reviewMessage.set(null), 5000);
      },
      error: (err) => {
        this.submittingReview.set(false);
        this.reviewMessage.set({
          type: 'error',
          text: 'Failed to record human review decision. Please check backend connection.'
        });
      }
    });
  }

  // Open & Handle Guideline Upload Modal
  openUploadModal(): void {
    this.showUploadModal.set(true);
    this.uploadError.set(null);
    this.uploadSuccess.set(null);
  }

  closeUploadModal(): void {
    this.showUploadModal.set(false);
    this.uploadFile = null;
    this.uploadTitle = '';
  }

  onFileChange(event: any): void {
    const file = event.target.files[0];
    if (file) {
      this.uploadFile = file;
      if (!this.uploadTitle) {
        this.uploadTitle = file.name.replace(/\.pdf$/i, '').replace(/_/g, ' ');
      }
    }
  }

  executeUpload(): void {
    if (!this.uploadFile) {
      this.uploadError.set('Please choose a valid PDF file.');
      return;
    }

    this.isUploading.set(true);
    this.uploadError.set(null);

    this.api.uploadGuideline(this.uploadFile, this.uploadTitle).subscribe({
      next: (res) => {
        this.isUploading.set(false);
        this.uploadSuccess.set(`Guideline "${res.title}" successfully uploaded and vector indexed!`);
        this.loadGuidelines();
        setTimeout(() => this.closeUploadModal(), 1800);
      },
      error: (err) => {
        this.isUploading.set(false);
        this.uploadError.set(err.error?.error || 'Failed to upload guideline.');
      }
    });
  }

  // Open & Handle Compare Content Modal
  openCompareModal(): void {
    this.showCompareModal.set(true);
  }

  closeCompareModal(): void {
    this.showCompareModal.set(false);
  }

  loadSamplePrompt(type: 'violating' | 'compliant' | 'medical'): void {
    if (type === 'violating') {
      this.newTitle = 'Express Performance Guarantee Campaign';
      this.newContentType = 'SocialMediaPost';
      this.newContentText = 'Our product guarantees 100% results and is the best solution in the world.';
    } else if (type === 'compliant') {
      this.newTitle = 'Enterprise Brand Workflow Campaign';
      this.newContentType = 'EmailNewsletter';
      this.newContentText = 'Our product is designed to help teams improve their marketing workflows with measurable efficiency.';
    } else {
      this.newTitle = 'Clinical Care Supplement Announcement';
      this.newContentType = 'BlogArticle';
      this.newContentText = 'Our miracle cure eliminates disease with zero risk and guarantees profit.';
    }
  }

  validateNewContent(): void {
    if (!this.newTitle.trim() || !this.newContentText.trim()) return;

    this.isValidatingNew.set(true);

    this.api.createSubmission({
      title: this.newTitle.trim(),
      contentType: this.newContentType,
      contentText: this.newContentText.trim()
    }).subscribe({
      next: (sub) => {
        this.api.validateContent(sub.id).subscribe({
          next: (run) => {
            this.isValidatingNew.set(false);
            this.closeCompareModal();
            this.loadSubmissions();
            this.selectedSubmissionId.set(sub.id);
            this.validationResult.set(run);
            this.activeTab.set('summary');
          },
          error: (err) => {
            this.isValidatingNew.set(false);
            console.error('Validation execution error', err);
          }
        });
      },
      error: (err) => {
        this.isValidatingNew.set(false);
        console.error('Submission creation error', err);
      }
    });
  }

  // Guideline Details Inspector Modal
  viewGuidelineDetails(guideline: GuidelineDto): void {
    this.api.getGuidelineById(guideline.id).subscribe({
      next: (detail) => this.viewingGuideline.set(detail),
      error: (err) => console.error('Failed to view guideline', err)
    });
  }

  closeGuidelineInspector(): void {
    this.viewingGuideline.set(null);
  }
}
