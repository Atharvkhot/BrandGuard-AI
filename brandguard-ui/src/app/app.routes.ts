import { Routes } from '@angular/router';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { GuidelinesComponent } from './components/guidelines/guidelines.component';
import { ContentSubmissionComponent } from './components/content-submission/content-submission.component';
import { ValidationResultsComponent } from './components/validation-results/validation-results.component';
import { HumanReviewComponent } from './components/human-review/human-review.component';
import { ReviewHistoryComponent } from './components/review-history/review-history.component';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: 'dashboard', component: DashboardComponent, title: 'Dashboard — BrandGuard AI' },
  { path: 'guidelines', component: GuidelinesComponent, title: 'Brand Guidelines — BrandGuard AI' },
  { path: 'submit', component: ContentSubmissionComponent, title: 'Submit Content — BrandGuard AI' },
  { path: 'results/:id', component: ValidationResultsComponent, title: 'Validation Results — BrandGuard AI' },
  { path: 'review/:id', component: HumanReviewComponent, title: 'Human Review — BrandGuard AI' },
  { path: 'history', component: ReviewHistoryComponent, title: 'Review History — BrandGuard AI' },
  { path: '**', redirectTo: 'dashboard' }
];
