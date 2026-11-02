import { Component, signal, inject, OnInit, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive, Router, NavigationEnd } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { filter } from 'rxjs/operators';
import { ApiService } from './services/api.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  private api = inject(ApiService);
  private router = inject(Router);

  readonly title = signal('BrandGuard AI');
  protected readonly backendOnline = signal<boolean>(true);
  protected readonly latestSubmissionId = signal<string | null>(null);
  protected readonly searchQuery = signal<string>('');
  protected readonly mobileMenuOpen = signal<boolean>(false);

  ngOnInit(): void {
    this.refreshLatestSubmission();

    // Close mobile drawer on route change
    this.router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => {
        this.closeMobileMenu();
      });
  }

  refreshLatestSubmission(): void {
    this.api.getSubmissions().subscribe({
      next: (submissions) => {
        if (submissions && submissions.length > 0) {
          this.latestSubmissionId.set(submissions[0].id);
        }
      },
      error: (err) => console.warn('Could not load latest submission ID', err)
    });
  }

  navigateToValidation(): void {
    const id = this.latestSubmissionId();
    if (id) {
      this.router.navigate(['/results', id]);
    } else {
      this.router.navigate(['/submit']);
    }
  }

  navigateToReview(): void {
    const id = this.latestSubmissionId();
    if (id) {
      this.router.navigate(['/review', id]);
    } else {
      this.router.navigate(['/history']);
    }
  }

  toggleMobileMenu(): void {
    this.mobileMenuOpen.update(v => {
      const next = !v;
      if (next) {
        document.body.classList.add('mobile-drawer-open');
      } else {
        document.body.classList.remove('mobile-drawer-open');
      }
      return next;
    });
  }

  closeMobileMenu(): void {
    if (this.mobileMenuOpen()) {
      this.mobileMenuOpen.set(false);
      document.body.classList.remove('mobile-drawer-open');
    }
  }

  @HostListener('window:keydown.escape')
  onEscapePress(): void {
    this.closeMobileMenu();
  }

  onSearch(event: Event): void {
    const input = (event.target as HTMLInputElement).value;
    this.searchQuery.set(input);
  }
}

