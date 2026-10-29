import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ApiService } from '../../services/api.service';
import { ReviewHistoryItemDto } from '../../models/brandguard.models';

@Component({
  selector: 'app-review-history',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './review-history.component.html',
  styleUrls: ['./review-history.component.scss']
})
export class ReviewHistoryComponent implements OnInit {
  private api = inject(ApiService);

  history = signal<ReviewHistoryItemDto[]>([]);
  loading = signal<boolean>(true);
  errorMessage = signal<string | null>(null);

  selectedFilter = signal<string>('ALL');

  filteredHistory = computed(() => {
    const list = this.history();
    const filter = this.selectedFilter().toUpperCase();
    if (filter === 'ALL') return list;
    return list.filter(item => {
      const dec = (item.decision || '').replace(/\s+/g, '').toUpperCase();
      return dec === filter;
    });
  });

  ngOnInit(): void {
    this.loadHistory();
  }

  formatContentType(type?: string): string {
    if (!type) return 'General';
    const normalized = type.replace(/\s+/g, '');
    if (normalized === 'SocialMediaPost') return 'Social Media Post';
    if (normalized === 'EmailNewsletter' || normalized === 'EmailCampaign') return 'Email Newsletter';
    if (normalized === 'BlogArticle') return 'Blog Article';
    if (normalized === 'LandingPage' || normalized === 'WebsiteContent') return 'Website / Landing Page';
    if (normalized === 'PressRelease') return 'Press Release';
    if (normalized === 'ProductDescription') return 'Product Description';
    return type;
  }

  loadHistory(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    forkJoin({
      submissions: this.api.getSubmissions().pipe(catchError(() => of([]))),
      reviews: this.api.getReviewHistory().pipe(catchError(() => of([])))
    }).subscribe({
      next: ({ submissions, reviews }) => {
        const reviewMap = new Map<string, ReviewHistoryItemDto>();
        for (const rev of reviews) {
          if (rev.contentSubmissionId && !reviewMap.has(rev.contentSubmissionId)) {
            reviewMap.set(rev.contentSubmissionId, rev);
          }
        }

        const combined: ReviewHistoryItemDto[] = [];
        const seenIds = new Set<string>();

        // Include all submissions returned by the API (including PendingReview)
        for (const sub of submissions) {
          seenIds.add(sub.id);
          const rev = reviewMap.get(sub.id);
          if (rev) {
            combined.push({
              id: rev.id,
              contentSubmissionId: sub.id,
              validationRunId: rev.validationRunId || sub.latestValidationRunId,
              contentTitle: sub.title || rev.contentTitle,
              contentType: sub.contentType || rev.contentType,
              reviewerName: rev.reviewerName,
              decision: rev.decision,
              comments: rev.comments,
              reviewedAt: rev.reviewedAt,
              overallRiskLevel: rev.overallRiskLevel || sub.latestRiskLevel,
              totalFindings: sub.totalFindings ?? rev.totalFindings ?? 0
            });
          } else {
            combined.push({
              id: sub.id,
              contentSubmissionId: sub.id,
              validationRunId: sub.latestValidationRunId,
              contentTitle: sub.title,
              contentType: sub.contentType,
              reviewerName: sub.status === 'PendingReview' ? 'Pending Review' : '—',
              decision: sub.status || 'PendingReview',
              comments: sub.status === 'PendingReview' ? 'Awaiting human review' : '—',
              reviewedAt: sub.createdAt,
              overallRiskLevel: sub.latestRiskLevel,
              totalFindings: sub.totalFindings ?? 0
            });
          }
        }

        // Include any remaining review items
        for (const rev of reviews) {
          if (rev.contentSubmissionId && !seenIds.has(rev.contentSubmissionId)) {
            combined.push(rev);
          }
        }

        this.history.set(combined);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load review history from database.', err);
        this.errorMessage.set('Failed to load review history from database.');
        this.loading.set(false);
      }
    });
  }
}
