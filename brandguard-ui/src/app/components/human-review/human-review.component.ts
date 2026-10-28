import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../../services/api.service';
import {
  ValidationResultDto,
  ReviewDecisionType,
  SubmitReviewDecisionDto,
  ReviewDecisionDto
} from '../../models/brandguard.models';

@Component({
  selector: 'app-human-review',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './human-review.component.html',
  styleUrls: ['./human-review.component.scss']
})
export class HumanReviewComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private api = inject(ApiService);

  submissionId = '';
  validationResult = signal<ValidationResultDto | null>(null);
  loading = signal<boolean>(true);
  submitting = signal<boolean>(false);
  errorMessage = signal<string | null>(null);
  existingDecision = signal<ReviewDecisionDto | null>(null);

  reviewerName = 'Judge Reviewer';
  selectedDecision: ReviewDecisionType = 'ChangesRequested';
  comments = '';

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

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      this.submissionId = params['id'];
      if (this.submissionId) {
        this.loadDetails();
      }
    });
  }

  loadDetails(): void {
    this.loading.set(true);

    // Load validation results
    this.api.getValidationResult(this.submissionId).subscribe({
      next: (val) => {
        this.validationResult.set(val);
        // Pre-select decision based on risk
        if (val.overallRiskLevel === 'High') {
          this.selectedDecision = 'ChangesRequested';
          this.comments = 'Please resolve the high-risk absolute guarantee and superlative claims before final publishing.';
        } else if (val.overallRiskLevel === 'Low') {
          this.selectedDecision = 'Approved';
          this.comments = 'Verified and approved according to current brand guidelines.';
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });

    // Check if decision already made
    this.api.getDecisionBySubmissionId(this.submissionId).subscribe({
      next: (dec) => {
        if (dec) {
          this.existingDecision.set(dec);
          this.reviewerName = dec.reviewerName;
          this.selectedDecision = dec.decision;
          this.comments = dec.comments;
        }
      },
      error: () => {}
    });
  }

  submitDecision(): void {
    if (!this.reviewerName.trim()) {
      this.errorMessage.set('Reviewer name is required for governance auditability.');
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    const dto: SubmitReviewDecisionDto = {
      contentSubmissionId: this.submissionId,
      validationRunId: this.validationResult()?.validationRunId,
      reviewerName: this.reviewerName.trim(),
      decision: this.selectedDecision,
      comments: this.comments.trim()
    };

    this.api.submitReviewDecision(dto).subscribe({
      next: (result) => {
        this.submitting.set(false);
        this.existingDecision.set(result);
        // Navigate to review history
        this.router.navigate(['/history']);
      },
      error: (err) => {
        this.submitting.set(false);
        this.errorMessage.set(err.error?.error || 'Failed to persist review decision.');
      }
    });
  }
}
