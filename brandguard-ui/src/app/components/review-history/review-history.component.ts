import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
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
    const filter = this.selectedFilter();
    if (filter === 'ALL') return list;
    return list.filter(item => item.decision.toUpperCase() === filter);
  });

  ngOnInit(): void {
    this.loadHistory();
  }

  loadHistory(): void {
    this.loading.set(true);
    this.api.getReviewHistory().subscribe({
      next: (data) => {
        this.history.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.errorMessage.set('Failed to load review history from database.');
        this.loading.set(false);
      }
    });
  }
}
