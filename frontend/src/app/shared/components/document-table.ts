import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { TranslationService } from '../../core/i18n/translation.service';
import { IdCard, Passport } from '../../core/models/person.model';
import { StatusBadge } from './status-badge';
import { AppDatePipe } from '../app-date.pipe';

interface DocumentRow {
  number: string;
  subType: string;
  status: string;
  issueDate: string | null;
  expiryDate: string | null;
}

@Component({
  selector: 'app-document-table',
  imports: [StatusBadge, AppDatePipe],
  templateUrl: './document-table.html',
  styleUrl: './document-table.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentTable {
  protected readonly i18n = inject(TranslationService);

  readonly idCards = input<IdCard[]>([]);
  readonly passports = input<Passport[]>([]);

  readonly idCardRows = computed<DocumentRow[]>(() =>
    this.idCards().map((card) => ({
      number: card.cardNumber,
      subType: card.cardType,
      status: card.status,
      issueDate: card.issueDate,
      expiryDate: card.expiryDate,
    })),
  );

  readonly passportRows = computed<DocumentRow[]>(() =>
    this.passports().map((passport) => ({
      number: passport.passportNumber,
      subType: passport.passportType,
      status: passport.status,
      issueDate: passport.issueDate,
      expiryDate: passport.expiryDate,
    })),
  );
}
