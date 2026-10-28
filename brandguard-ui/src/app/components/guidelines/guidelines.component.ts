import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { GuidelineDto, GuidelineDetailDto, GuidelineChunkDto } from '../../models/brandguard.models';

@Component({
  selector: 'app-guidelines',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './guidelines.component.html',
  styleUrls: ['./guidelines.component.scss']
})
export class GuidelinesComponent implements OnInit {
  private api = inject(ApiService);

  guidelines = signal<GuidelineDto[]>([]);
  loading = signal<boolean>(true);
  uploading = signal<boolean>(false);
  uploadProgress = signal<number>(0);
  isDragging = signal<boolean>(false);

  selectedFile: File | null = null;
  guidelineTitle = '';
  errorMessage = signal<string | null>(null);
  successMessage = signal<string | null>(null);

  // Inspector modal state
  selectedGuideline = signal<GuidelineDetailDto | null>(null);
  inspectingChunks = signal<boolean>(false);
  chunksList = signal<GuidelineChunkDto[]>([]);

  ngOnInit(): void {
    this.loadGuidelines();
  }

  loadGuidelines(): void {
    this.loading.set(true);
    this.api.getGuidelines().subscribe({
      next: (data) => {
        this.guidelines.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.errorMessage.set('Failed to fetch brand guidelines from backend.');
        this.loading.set(false);
      }
    });
  }

  onFileSelected(event: any): void {
    const file = event.target.files[0];
    this.handleFile(file);
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging.set(false);

    if (event.dataTransfer && event.dataTransfer.files.length > 0) {
      this.handleFile(event.dataTransfer.files[0]);
    }
  }

  handleFile(file: File): void {
    this.errorMessage.set(null);
    if (!file) return;

    if (!file.name.toLowerCase().endsWith('.pdf')) {
      this.errorMessage.set('Invalid file type. Only PDF documents are supported for brand guidelines.');
      this.selectedFile = null;
      return;
    }

    if (file.size > 25 * 1024 * 1024) {
      this.errorMessage.set('File is too large. Maximum permitted size is 25MB.');
      this.selectedFile = null;
      return;
    }

    this.selectedFile = file;
    if (!this.guidelineTitle) {
      this.guidelineTitle = file.name.replace(/\.[^/.]+$/, '');
    }
  }

  submitUpload(): void {
    if (!this.selectedFile) return;

    this.uploading.set(true);
    this.uploadProgress.set(25);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    const progressTimer = setInterval(() => {
      if (this.uploadProgress() < 90) {
        this.uploadProgress.update(p => p + 15);
      }
    }, 250);

    this.api.uploadGuideline(this.selectedFile, this.guidelineTitle).subscribe({
      next: (result) => {
        clearInterval(progressTimer);
        this.uploadProgress.set(100);
        setTimeout(() => {
          this.uploading.set(false);
          this.selectedFile = null;
          this.guidelineTitle = '';
          this.successMessage.set(`Successfully uploaded and indexed '${result.title}' into ${result.chunkCount} semantic vector chunks!`);
          this.loadGuidelines();
          setTimeout(() => this.successMessage.set(null), 5000);
        }, 500);
      },
      error: (err) => {
        clearInterval(progressTimer);
        this.uploading.set(false);
        this.errorMessage.set(err.error?.error || 'Failed to upload and index PDF.');
      }
    });
  }

  inspectGuideline(id: string): void {
    this.inspectingChunks.set(true);
    this.api.getGuidelineById(id).subscribe({
      next: (data) => {
        this.selectedGuideline.set(data);
        this.chunksList.set(data.chunks || []);
      },
      error: (err) => {
        this.errorMessage.set('Unable to retrieve guideline details.');
      }
    });
  }

  closeInspector(): void {
    this.selectedGuideline.set(null);
    this.inspectingChunks.set(false);
    this.chunksList.set([]);
  }

  downloadSample(): void {
    window.open(this.api.getSamplePdfUrl(), '_blank');
  }
}
