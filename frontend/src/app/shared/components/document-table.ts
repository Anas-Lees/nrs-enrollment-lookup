import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { TranslationService } from '../../core/i18n/translation.service';
import { IdCard, Passport } from '../../core/models/person.model';
import { StatusBadge } from './status-badge';

interface DocumentRow {
  kind: 'ID Card' | 'Passport';
  number: string;
  subType: string;
  status: string;
  issueDate: string | null;
  expiryDate: string | null;
}

@Component({
  selector: 'app-document-table',
  imports: [DatePipe, StatusBadge],
  templateUrl: './document-table.html',
  styleUrl: './document-table.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentTable {
  protected readonly i18n = inject(TranslationService);

  readonly idCards = input<IdCard[]>([]);
  readonly passports = input<Passport[]>([]);

  readonly rows = computed<DocumentRow[]>(() => {
    const cardRows: DocumentRow[] = this.idCards().map((card) => ({
      kind: 'ID Card',
      number: card.cardNumber,
      subType: card.cardType,
      status: card.status,
      issueDate: card.issueDate,
      expiryDate: card.expiryDate,
    }));

    const passportRows: DocumentRow[] = this.passports().map((passport) => ({
      kind: 'Passport',
      number: passport.passportNumber,
      subType: passport.passportType,
      status: passport.status,
      issueDate: passport.issueDate,
      expiryDate: passport.expiryDate,
    }));

    return [...cardRows, ...passportRows];
  });
}
