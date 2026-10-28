import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { ValidationResultDto, FindingDto } from '../../models/brandguard.models';

export type ViewMode = 'standard' | 'guided' | 'batch';

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

  // View Mode: 'standard' | 'guided' | 'batch'
  activeMode = signal<ViewMode>('standard');

  // Guided Focus Mode State
  currentFocusIndex = signal<number>(0);

  // Quick Batch Mode State
  selectedFindingIds = new Set<string>();
  batchMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      this.submissionId = params['id'];
      if (this.submissionId) {
        this.loadValidationResults();
      }
    });
  }

  loadValidationResults(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.api.getValidationResult(this.submissionId).subscribe({
      next: (result) => {
        this.validationResult.set(result);
        this.loading.set(false);
      },
      error: (err) => {
        // If not yet validated, attempt validation now
        this.api.validateContent(this.submissionId).subscribe({
          next: (run) => {
            this.validationResult.set(run);
            this.loading.set(false);
          },
          error: (valErr) => {
            this.errorMessage.set('Unable to retrieve or execute validation results for this submission.');
            this.loading.set(false);
          }
        });
      }
    });
  }

  setMode(mode: ViewMode): void {
    this.activeMode.set(mode);
    this.currentFocusIndex.set(0);
    this.batchMessage.set(null);
  }

  // Guided Focus Mode Navigation
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

  // Quick Batch Mode Actions
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

  acknowledgeSelected(): void {
    if (this.selectedFindingIds.size === 0) return;

    const result = this.validationResult();
    if (!result) return;

    result.findings.forEach(f => {
      if (this.selectedFindingIds.has(f.id)) {
        f.acknowledged = true;
      }
    });

    this.batchMessage.set(`Acknowledged ${this.selectedFindingIds.size} findings for human editorial consideration.`);
    this.selectedFindingIds.clear();
    setTimeout(() => this.batchMessage.set(null), 4000);
  }

  goToReview(): void {
    this.router.navigate(['/review', this.submissionId]);
  }
}
