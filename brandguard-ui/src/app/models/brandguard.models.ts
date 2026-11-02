export type RiskSeverity = 'Low' | 'Medium' | 'High';
export type GuidelineStatus = 'Uploading' | 'Processing' | 'Ready' | 'Failed';
export type ContentSubmissionStatus = 'Draft' | 'Validating' | 'PendingReview' | 'Approved' | 'ChangesRequested' | 'Rejected';
export type ReviewDecisionType = 'Approved' | 'ChangesRequested' | 'Rejected';

export interface GuidelineDto {
  id: string;
  title: string;
  fileName: string;
  status: GuidelineStatus;
  uploadedAt: string;
  isActive: boolean;
  chunkCount: number;
  textLength: number;
}

export interface GuidelineChunkDto {
  id: string;
  brandGuidelineId: string;
  chunkIndex: number;
  chunkText: string;
  pageNumber: number;
  sectionTitle: string;
  createdAt: string;
}

export interface GuidelineDetailDto {
  id: string;
  title: string;
  fileName: string;
  filePath: string;
  extractedText: string;
  status: GuidelineStatus;
  uploadedAt: string;
  isActive: boolean;
  chunks: GuidelineChunkDto[];
}

export interface CreateContentSubmissionDto {
  title: string;
  contentType: string;
  contentText: string;
}

export interface ContentSubmissionDto {
  id: string;
  title: string;
  contentType: string;
  contentText: string;
  status: ContentSubmissionStatus;
  createdAt: string;
  updatedAt: string;
  latestValidationRunId?: string;
  latestRiskLevel?: RiskSeverity;
  totalFindings: number;
}

export type EvidenceProvenance = 'DirectGuidelineMatch' | 'DeterministicRule' | 'UnverifiedAIClaim' | 'NoEvidence';

export interface FindingDto {
  id: string;
  validationRunId: string;
  guidelineChunkId?: string;
  issueType: string;
  severity: RiskSeverity;
  originalText: string;
  explanation: string;
  suggestedFix: string;
  evidenceText: string;
  sourceGuidelineTitle?: string;
  pageNumber?: number;
  sectionTitle?: string;
  createdAt: string;
  acknowledged?: boolean;
  isAcknowledged?: boolean;
  acknowledgedAt?: string;
  acknowledgedBy?: string;
  evidenceProvenance?: EvidenceProvenance;
  hasVerifiedEvidence?: boolean;
}

export interface ValidationResultDto {
  validationRunId: string;
  contentSubmissionId: string;
  submissionTitle: string;
  contentType: string;
  originalContent: string;
  status: string;
  overallRiskLevel: RiskSeverity;
  totalFindings: number;
  highRiskCount: number;
  mediumRiskCount: number;
  lowRiskCount: number;
  startedAt: string;
  completedAt?: string;
  errorMessage?: string;
  findings: FindingDto[];
}

export interface SubmitReviewDecisionDto {
  contentSubmissionId: string;
  validationRunId?: string;
  reviewerName: string;
  decision: ReviewDecisionType;
  comments: string;
}

export interface ReviewDecisionDto {
  id: string;
  contentSubmissionId: string;
  validationRunId?: string;
  contentTitle: string;
  contentType: string;
  reviewerName: string;
  decision: ReviewDecisionType;
  comments: string;
  reviewedAt: string;
}

export interface ReviewHistoryItemDto {
  id: string;
  contentSubmissionId: string;
  validationRunId?: string;
  contentTitle: string;
  contentType: string;
  reviewerName: string;
  decision: ReviewDecisionType | ContentSubmissionStatus | string;
  comments: string;
  reviewedAt: string;
  overallRiskLevel?: RiskSeverity;
  totalFindings: number;
}

export interface RecentSubmissionItemDto {
  id: string;
  title: string;
  contentType: string;
  createdAt: string;
  riskLevel?: RiskSeverity;
  status: ContentSubmissionStatus;
  totalFindings: number;
  latestValidationRunId?: string;
}

export interface DashboardStatsDto {
  totalGuidelines: number;
  totalContentSubmissions: number;
  pendingReviews: number;
  approvedSubmissions: number;
  rejectedSubmissions: number;
  changesRequestedSubmissions: number;
  highRiskFindings: number;
  mediumRiskFindings: number;
  lowRiskFindings: number;
  recentSubmissions: RecentSubmissionItemDto[];
}

export interface AcknowledgeFindingsRequest {
  findingIds: string[];
  acknowledgedBy?: string;
}

export interface AcknowledgeFindingsResultDto {
  acknowledgedCount: number;
  acknowledgedFindingIds: string[];
}

export interface BatchReviewDecisionDto {
  contentSubmissionIds: string[];
  decision: ReviewDecisionType;
  reviewerName: string;
  comments: string;
  confirmHighRiskApproval?: boolean;
}

export interface BatchReviewItemResultDto {
  contentSubmissionId: string;
  title: string;
  success: boolean;
  errorMessage?: string;
  decision?: ReviewDecisionType;
}

export interface BatchReviewResultDto {
  totalProcessed: number;
  succeeded: number;
  failed: number;
  results: BatchReviewItemResultDto[];
}

export interface GovernanceOverviewDto {
  totalSubmissions: number;
  totalValidationRuns: number;
  totalFindings: number;
  highRiskFindings: number;
  mediumRiskFindings: number;
  lowRiskFindings: number;
  pendingReviews: number;
  approvedSubmissions: number;
  rejectedSubmissions: number;
  changesRequestedSubmissions: number;
  totalGuidelines: number;
}

export interface ApprovalAnalyticsDto {
  totalReviewedSubmissions: number;
  approvedCount: number;
  rejectedCount: number;
  changesRequestedCount: number;
  approvalRate: number;
  rejectionRate: number;
  changesRequestedRate: number;
  totalReviewEvents: number;
}

export interface DailyViolationTrendDto {
  date: string;
  totalFindings: number;
  highRiskCount: number;
  mediumRiskCount: number;
  lowRiskCount: number;
}

export interface ViolatedGuidelineDto {
  guidelineOrRuleName: string;
  violationCount: number;
  percentageOfTotal: number;
  highRiskCount: number;
  mediumRiskCount: number;
  lowRiskCount: number;
  isVerifiedGuideline: boolean;
  isDeterministicRule: boolean;
}

export interface GovernanceHealthScoreDto {
  score: number | null;
  statusLabel: string;
  formulaExplanation: string;
  baseScore: number;
  findingPenalty: number;
  decisionAdjustment: number;
  acknowledgmentBonus: number;
  isIndicatorOnly: boolean;
  hasSufficientData?: boolean;
}

export interface GovernanceActivityDto {
  id: string;
  activityType: 'Validation' | 'HumanReview' | string;
  timestamp: string;
  title: string;
  details: string;
  severity?: string;
  reviewerOrActor?: string;
}

export interface GovernanceIntelligenceDto {
  overview: GovernanceOverviewDto;
  approvalAnalytics: ApprovalAnalyticsDto;
  violationTrends: DailyViolationTrendDto[];
  topViolatedGuidelines: ViolatedGuidelineDto[];
  healthScore: GovernanceHealthScoreDto;
  recentActivities: GovernanceActivityDto[];
  selectedDays: number;
  generatedAt: string;
}

