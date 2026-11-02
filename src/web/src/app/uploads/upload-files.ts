import { Component, computed, effect, inject, OnDestroy } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router } from '@angular/router';
import { AUDIO_ACCEPT, AUDIO_FORMATS_LABEL } from './audio-formats';
import { TEXT_ACCEPT, TEXT_FORMATS_LABEL, TEXT_LIMITS_LABEL } from './text-file';
import { proposedTitle, UploadQueue } from './upload-queue';

/**
 * Pagina "Carica file": la scelta dei file, l'anteprima con titolo e data modificabili (Salva o
 * Annulla) e la lista degli upload, ciascuno con la X per annullarlo. Gli upload confermati continuano
 * lasciando la pagina; l'anteprima non confermata no. Quando la lista si svuota si torna alla homepage.
 */
@Component({
  selector: 'app-upload-files',
  imports: [
    DecimalPipe,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatListModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './upload-files.html',
  styleUrl: './upload-files.scss',
})
export class UploadFiles implements OnDestroy {
  private readonly router = inject(Router);
  protected readonly queue = inject(UploadQueue);

  protected readonly uploadAccept = `${AUDIO_ACCEPT},${TEXT_ACCEPT}`;
  protected readonly formats = `${AUDIO_FORMATS_LABEL}, ${TEXT_FORMATS_LABEL}`;
  protected readonly textLimits = TEXT_LIMITS_LABEL;
  protected readonly proposedTitle = proposedTitle;

  protected readonly canSave = computed(
    () =>
      this.queue.preview().some((item) => item.status === 'preview') &&
      !this.queue.preview().some((item) => item.status === 'checking'),
  );

  constructor() {
    let hadItems = this.queue.items().length > 0;
    effect(() => {
      const hasItems = this.queue.items().length > 0;
      if (hadItems && !hasItems) {
        void this.router.navigate(['/']);
      }
      hadItems = hasItems;
    });
  }

  ngOnDestroy(): void {
    this.queue.discardPreview();
  }

  protected onFileInput(input: HTMLInputElement): void {
    const files = Array.from(input.files ?? []);
    input.value = '';
    if (files.length > 0) {
      this.queue.add(files);
    }
  }

  protected backToNotes(): void {
    this.queue.clearFinished();
    void this.router.navigate(['/']);
  }
}
