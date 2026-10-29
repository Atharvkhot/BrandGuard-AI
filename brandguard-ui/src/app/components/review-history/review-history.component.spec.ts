import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ReviewHistoryComponent } from './review-history.component';
import { ApiService } from '../../services/api.service';
import { ContentSubmissionDto, ReviewHistoryItemDto } from '../../models/brandguard.models';

describe('ReviewHistoryComponent - Missing Submission & Filter Coverage', () => {
  let component: ReviewHistoryComponent;
  let fixture: ComponentFixture<ReviewHistoryComponent>;

  const mockSubmissions: ContentSubmissionDto[] = [
    {
      id: 'bef451ae-c700-4565-94dd-834e1213e95c',
      title: 'NOVA GREEN – Environmental Claims Compliance Test',
      contentType: 'Social Media Post',
      contentText: 'Sample eco-friendly claims for test campaign.',
      status: 'PendingReview',
      latestRiskLevel: 'High',
      totalFindings: 5,
      createdAt: '2026-10-27T10:00:00Z',
      updatedAt: '2026-10-27T10:05:00Z',
      latestValidationRunId: 'val-run-nova-green'
    },
    {
      id: 'c23e4567-e89b-12d3-a456-426614174000',
      title: 'Spring 2026 Social Campaign',
      contentType: 'SocialMediaPost',
      contentText: 'Spring campaign content with minor warnings.',
      status: 'Approved',
      latestRiskLevel: 'Low',
      totalFindings: 1,
      createdAt: '2026-10-26T12:00:00Z',
      updatedAt: '2026-10-26T14:30:00Z',
      latestValidationRunId: 'val-run-spring-2026'
    }
  ];

  const mockReviews: ReviewHistoryItemDto[] = [
    {
      id: 'rev-001',
      contentSubmissionId: 'c23e4567-e89b-12d3-a456-426614174000',
      validationRunId: 'val-run-spring-2026',
      contentTitle: 'Spring 2026 Social Campaign',
      contentType: 'SocialMediaPost',
      reviewerName: 'Lead Reviewer',
      decision: 'Approved',
      comments: 'Approved with conditions.',
      reviewedAt: '2026-10-26T14:30:00Z',
      overallRiskLevel: 'Low',
      totalFindings: 1
    }
  ];

  const apiServiceMock = {
    getSubmissions: () => of(mockSubmissions),
    getReviewHistory: () => of(mockReviews)
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReviewHistoryComponent],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        { provide: ApiService, useValue: apiServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ReviewHistoryComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the review history component', () => {
    expect(component).toBeTruthy();
  });

  it('should display all submissions returned by the API when no filter is active (ALL)', () => {
    expect(component.selectedFilter()).toBe('ALL');
    const filtered = component.filteredHistory();
    expect(filtered.length).toBe(2);

    // Verify NOVA GREEN is present despite being PendingReview with no prior human review row
    const novaGreen = filtered.find(item => item.contentSubmissionId === 'bef451ae-c700-4565-94dd-834e1213e95c');
    expect(novaGreen).toBeDefined();
    expect(novaGreen?.contentTitle).toBe('NOVA GREEN – Environmental Claims Compliance Test');
    expect(novaGreen?.decision).toBe('PendingReview');
    expect(novaGreen?.overallRiskLevel).toBe('High');
    expect(novaGreen?.totalFindings).toBe(5);
  });

  it('should correctly filter for Pending Review submissions', () => {
    component.selectedFilter.set('PENDINGREVIEW');
    fixture.detectChanges();

    const filtered = component.filteredHistory();
    expect(filtered.length).toBe(1);
    expect(filtered[0].contentTitle).toBe('NOVA GREEN – Environmental Claims Compliance Test');
    expect(filtered[0].decision).toBe('PendingReview');
  });

  it('should correctly filter for Approved submissions', () => {
    component.selectedFilter.set('APPROVED');
    fixture.detectChanges();

    const filtered = component.filteredHistory();
    expect(filtered.length).toBe(1);
    expect(filtered[0].contentTitle).toBe('Spring 2026 Social Campaign');
    expect(filtered[0].decision).toBe('Approved');
  });

  it('should correctly format content types for Social Media Post and SocialMediaPost', () => {
    expect(component.formatContentType('Social Media Post')).toBe('Social Media Post');
    expect(component.formatContentType('SocialMediaPost')).toBe('Social Media Post');
    expect(component.formatContentType('EmailCampaign')).toBe('Email Newsletter');
    expect(component.formatContentType(undefined)).toBe('General');
  });

  it('should render NOVA GREEN in the DOM table with pending review indicators', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const tableText = compiled.textContent || '';

    expect(tableText).toContain('NOVA GREEN – Environmental Claims Compliance Test');
    expect(tableText).toContain('Spring 2026 Social Campaign');
    expect(tableText).toContain('Pending Review');
  });
});
