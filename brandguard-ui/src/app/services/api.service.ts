import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  GuidelineDto,
  GuidelineDetailDto,
  GuidelineChunkDto,
  CreateContentSubmissionDto,
  ContentSubmissionDto,
  ValidationResultDto,
  SubmitReviewDecisionDto,
  ReviewDecisionDto,
  ReviewHistoryItemDto,
  DashboardStatsDto
} from '../models/brandguard.models';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private http = inject(HttpClient);
  // Default to relative or port 5000 / localhost
  private readonly baseUrl = 'http://localhost:5000/api';

  // --- Dashboard ---
  getDashboardStats(): Observable<DashboardStatsDto> {
    return this.http.get<DashboardStatsDto>(`${this.baseUrl}/dashboard/stats`);
  }

  // --- Guidelines ---
  getGuidelines(): Observable<GuidelineDto[]> {
    return this.http.get<GuidelineDto[]>(`${this.baseUrl}/guidelines`);
  }

  getGuidelineById(id: string): Observable<GuidelineDetailDto> {
    return this.http.get<GuidelineDetailDto>(`${this.baseUrl}/guidelines/${id}`);
  }

  getGuidelineChunks(id: string): Observable<GuidelineChunkDto[]> {
    return this.http.get<GuidelineChunkDto[]>(`${this.baseUrl}/guidelines/${id}/chunks`);
  }

  uploadGuideline(file: File, title?: string): Observable<GuidelineDto> {
    const formData = new FormData();
    formData.append('file', file);
    if (title) {
      formData.append('title', title);
    }
    return this.http.post<GuidelineDto>(`${this.baseUrl}/guidelines/upload`, formData);
  }

  deleteGuideline(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/guidelines/${id}`);
  }

  // --- Content Submissions ---
  createSubmission(dto: CreateContentSubmissionDto): Observable<ContentSubmissionDto> {
    return this.http.post<ContentSubmissionDto>(`${this.baseUrl}/content`, dto);
  }

  getSubmissions(): Observable<ContentSubmissionDto[]> {
    return this.http.get<ContentSubmissionDto[]>(`${this.baseUrl}/content`);
  }

  getSubmissionById(id: string): Observable<ContentSubmissionDto> {
    return this.http.get<ContentSubmissionDto>(`${this.baseUrl}/content/${id}`);
  }

  validateContent(submissionId: string): Observable<ValidationResultDto> {
    return this.http.post<ValidationResultDto>(`${this.baseUrl}/content/${submissionId}/validate`, {});
  }

  getValidationResult(submissionId: string): Observable<ValidationResultDto> {
    return this.http.get<ValidationResultDto>(`${this.baseUrl}/content/${submissionId}/validation-result`);
  }

  // --- Reviews ---
  submitReviewDecision(dto: SubmitReviewDecisionDto): Observable<ReviewDecisionDto> {
    return this.http.post<ReviewDecisionDto>(`${this.baseUrl}/reviews`, dto);
  }

  getReviewHistory(): Observable<ReviewHistoryItemDto[]> {
    return this.http.get<ReviewHistoryItemDto[]>(`${this.baseUrl}/reviews/history`);
  }

  getDecisionBySubmissionId(submissionId: string): Observable<ReviewDecisionDto> {
    return this.http.get<ReviewDecisionDto>(`${this.baseUrl}/reviews/submission/${submissionId}`);
  }

  // --- Demo & Testing Utilities ---
  seedDemoEnvironment(): Observable<any> {
    return this.http.post<any>(`${this.baseUrl}/demo/seed`, {});
  }

  getSamplePrompts(): Observable<any> {
    return this.http.get<any>(`${this.baseUrl}/demo/samples`);
  }

  getSamplePdfUrl(): string {
    return `${this.baseUrl}/demo/sample-pdf`;
  }
}
