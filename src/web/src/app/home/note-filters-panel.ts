import { Component, computed, effect, inject, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSliderModule } from '@angular/material/slider';
import {
  FilterPeriod,
  FilterSelection,
  FilterSourceType,
  NoteFilters,
  PERIOD_LABELS,
} from './note-filters';

/**
 * Pannello dei filtri della homepage, aperto dall'icona sopra la lista: tipologia, tag (più d'uno, in
 * OR), periodo (predefinito o personalizzato) e durata degli audio. Le scelte restano in una bozza
 * finché l'utente non tocca "Applica"; i filtri applicati compaiono come chip sotto la ricerca.
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
  /** Scelte non ancora applicate, partendo dai filtri applicati. */
  protected readonly draft = new FilterSelection(this.filters.maxDurationMinutes);

  /** Filtri applicati: il pannello si chiude. */
  readonly applied = output();

  protected readonly periods = Object.entries(PERIOD_LABELS) as [FilterPeriod, string][];

  /** Intervallo mostrato sopra lo slider: senza limiti, da 0 alla durata massima di un audio. */
  protected readonly durationText = computed(
    () =>
      `${this.draft.minMinutes() ?? 0}–${this.draft.maxMinutes() ?? this.filters.maxDurationMinutes} min`,
  );

  constructor() {
    // Anche togliendo una chip con il pannello aperto la bozza riparte dai filtri applicati.
    effect(() => this.draft.set(this.filters.values()));
  }

  protected setSourceType(value: FilterSourceType | undefined): void {
    this.draft.sourceType.set(value ?? null);
  }

  protected setPeriod(value: FilterPeriod | undefined): void {
    this.draft.setPeriod(value ?? null);
  }

  protected setMinDuration(value: number): void {
    this.draft.setDuration(value, this.draft.maxMinutes() ?? this.filters.maxDurationMinutes);
  }

  protected setMaxDuration(value: number): void {
    this.draft.setDuration(this.draft.minMinutes() ?? 0, value);
  }

  protected apply(): void {
    this.filters.set(this.draft.values());
    this.applied.emit();
  }
}
