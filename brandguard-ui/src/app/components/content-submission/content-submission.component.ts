import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { CreateContentSubmissionDto } from '../../models/brandguard.models';

@Component({
  selector: 'app-content-submission',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './content-submission.component.html',
  styleUrls: ['./content-submission.component.scss']
})
export class ContentSubmissionComponent implements OnInit {
  private api = inject(ApiService);
  private router = inject(Router);

  title = '';
  contentType = 'Social Media Post';
  contentText = '';

  submitting = signal<boolean>(false);
  validating = signal<boolean>(false);
  errorMessage = signal<string | null>(null);

  contentTypes = [
    'Social Media Post',
    'Advertisement',
    'Email Campaign',
    'Website Content',
    'Product Description'
  ];

  samplePrompts: any = null;

  ngOnInit(): void {
    this.api.getSamplePrompts().subscribe({
      next: (data) => this.samplePrompts = data,
      error: () => {}
    });
  }

  loadViolatingSample(): void {
    if (this.samplePrompts?.nonCompliantSample) {
      this.title = this.samplePrompts.nonCompliantSample.title;
      this.contentType = this.samplePrompts.nonCompliantSample.contentType;
      this.contentText = this.samplePrompts.nonCompliantSample.content;
    } else {
      this.title = 'Spring Product Launch — Hero Campaign';
      this.contentType = 'Social Media Post';
      this.contentText = 'Our product guarantees 100% results and is the best solution in the world. Eliminate all friction with our revolutionary platform today!';
    }
  }

  loadCompliantSample(): void {
    if (this.samplePrompts?.compliantSample) {
      this.title = this.samplePrompts.compliantSample.title;
      this.contentType = this.samplePrompts.compliantSample.contentType;
      this.contentText = this.samplePrompts.compliantSample.content;
    } else {
      this.title = 'Enterprise Governance Solution Overview';
      this.contentType = 'Website Content';
      this.contentText = 'BrandGuard AI provides comprehensive brand policy governance for enterprise marketing operations. Designed to streamline editorial review cycles and align communication with approved brand standards. Terms and conditions apply. Results may vary.';
    }
  }

  saveDraft(): void {
    if (!this.validateForm()) return;

    this.submitting.set(true);
    const dto: CreateContentSubmissionDto = {
      title: this.title.trim(),
      contentType: this.contentType,
      contentText: this.contentText.trim()
    };

    this.api.createSubmission(dto).subscribe({
      next: (res) => {
        this.submitting.set(false);
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.submitting.set(false);
        this.errorMessage.set(err.error?.error || 'Failed to save draft.');
      }
    });
  }

  submitAndValidate(): void {
    if (!this.validateForm()) return;

    this.validating.set(true);
    this.errorMessage.set(null);

    const dto: CreateContentSubmissionDto = {
      title: this.title.trim(),
      contentType: this.contentType,
      contentText: this.contentText.trim()
    };

    // 1. Create submission
    this.api.createSubmission(dto).subscribe({
      next: (submission) => {
        // 2. Trigger validation
        this.api.validateContent(submission.id).subscribe({
          next: (runResult) => {
            this.validating.set(false);
            // Navigate to results page
            this.router.navigate(['/results', submission.id]);
          },
          error: (valErr) => {
            this.validating.set(false);
            this.errorMessage.set(valErr.error?.error || 'Validation failed. Check guideline status or AI configuration.');
          }
        });
      },
      error: (subErr) => {
        this.validating.set(false);
        this.errorMessage.set(subErr.error?.error || 'Failed to create submission.');
      }
    });
  }

  private validateForm(): boolean {
    this.errorMessage.set(null);
    if (!this.title.trim()) {
      this.errorMessage.set('Please provide a submission title.');
      return false;
    }
    if (!this.contentText.trim()) {
      this.errorMessage.set('Marketing content text cannot be empty.');
      return false;
    }
    return true;
  }
}
