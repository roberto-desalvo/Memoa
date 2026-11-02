import { Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSliderModule } from '@angular/material/slider';
import { FilterPeriod, FilterSourceType, NoteFilters, PERIOD_LABELS } from './note-filters';

/**
 * Pannello dei filtri della homepage, aperto dall'icona sopra la lista: tipologia, tag (più d'uno, in
 * OR), periodo (predefinito o personalizzato) e durata degli audio. I filtri valgono subito, senza
 * conferma; quelli attivi compaiono come chip sotto la ricerca.
 */
@Component({
  selector: 'app-note-filters-panel',
  imports: [
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSliderModule,
  ],
  templateUrl: './note-filters-panel.html',
  styleUrl: './note-filters-panel.scss',
})
export class NoteFiltersPanel {
  protected readonly filters = inject(NoteFilters);

  protected readonly periods = Object.entries(PERIOD_LABELS) as [FilterPeriod, string][];

  /** Intervallo mostrato sopra lo slider: senza limiti, da 0 alla durata massima di un audio. */
  protected readonly durationText = computed(
    () =>
      `${this.filters.minMinutes() ?? 0}–${this.filters.maxMinutes() ?? this.filters.maxDurationMinutes} min`,
  );

  protected setSourceType(value: FilterSourceType | undefined): void {
    this.filters.sourceType.set(value ?? null);
  }

  protected setPeriod(value: FilterPeriod | undefined): void {
    this.filters.setPeriod(value ?? null);
  }

  protected setMinDuration(value: number): void {
    this.filters.setDuration(value, this.filters.maxMinutes() ?? this.filters.maxDurationMinutes);
  }

  protected setMaxDuration(value: number): void {
    this.filters.setDuration(this.filters.minMinutes() ?? 0, value);
  }
}
