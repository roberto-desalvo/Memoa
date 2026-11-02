/** Passo della guida: testo e, se c'è, l'elemento della homepage da evidenziare. */
export interface GuideStep {
  /**
   * Valore dell'attributo `data-guide` dell'elemento evidenziato. Null, o elemento assente o nascosto (per
   * esempio i filtri con il diario vuoto): il passo compare al centro senza evidenziazione.
   */
  anchor: string | null;
  title: string;
  text: string;
}

/** Passi della guida, tutti sulla homepage. */
export function guideSteps(maxAudioDurationMinutes: number): GuideStep[] {
  return [
    {
      anchor: null,
      title: 'Benvenuto in Memoa',
      text:
        'Memoa è il tuo diario: registra pensieri a voce, scrivi note o carica file, e ritrovali ' +
        'quando ti servono. Ecco come funziona, in pochi passi.',
    },
    {
      anchor: 'add',
      title: 'Aggiungi una nota',
      text:
        'Tocca + per scrivere una nota, registrare un audio o caricare file: audio fino a ' +
        `${maxAudioDurationMinutes} minuti e testi, anche DOCX e PDF. Gli audio vengono trascritti ` +
        'in automatico.',
    },
    {
      anchor: 'notes',
      title: 'Le tue note',
      text:
        'Qui trovi tutte le note, dalla più recente. Quelle appena aggiunte restano "In elaborazione" ' +
        'per qualche momento; se qualcosa va storto, aprile per riprovare. Scorri una nota verso ' +
        'destra per eliminarla, tienila premuta per selezionarne più di una.',
    },
    {
      anchor: 'search',
      title: 'Cerca per significato',
      text:
        'Scrivi cosa cerchi e premi Invio: Memoa trova le note che parlano di quell’argomento anche ' +
        'senza le stesse parole, e anche tra italiano e inglese.',
    },
    {
      anchor: 'filters',
      title: 'Filtra le note',
      text:
        'L’icona dei filtri, sopra la lista, restringe note e risultati della ricerca per tipologia, ' +
        'tag, periodo e durata degli audio. Compare appena hai la prima nota.',
    },
    {
      anchor: 'tags',
      title: 'Organizza con i tag',
      text:
        'Nella pagina Tag crei i tuoi tag, con una descrizione di ciò che rappresentano: Memoa li ' +
        'assegna da solo alle note che corrispondono. Puoi aggiungerli o toglierli a mano dalla ' +
        'pagina della nota.',
    },
    {
      anchor: 'user-menu',
      title: 'Il tuo account',
      text:
        'Dal menu utente trovi Impostazioni, Informazioni e la voce Guida, per rivedere questo tour ' +
        'quando vuoi.',
    },
  ];
}
