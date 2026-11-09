import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, Router } from '@angular/router';
import {
  catchError,
  EMPTY,
  exhaustMap,
  filter,
  finalize,
  Subscription,
  takeUntil,
} from 'rxjs';
import { SaveBookingUseCase } from '../../../../domain/use-cases/save-booking.usecase';
import { BookingModal } from '../../../components/booking-modal/booking-modal.component';
import { ServiceDetailCardComponent } from '../../../components/service-detail-card/service-detail-card.component';
import { BookingStore } from '../../../state/booking.store';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { BookingRow } from '../../../../domain/models/booking-row.model';
import { CommonModule } from '@angular/common';
import { GetBookingsByServiceUseCase } from '../../../../domain/use-cases/get-bookings-by-service.usecase';

@Component({
  selector: 'app-service-detail',
  imports: [
    CommonModule,
    ServiceDetailCardComponent,
    MatSelectModule,
    MatInputModule,
    MatFormFieldModule,
    MatDatepickerModule,
    FormsModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatTableModule,
  ],
  providers: [provideNativeDateAdapter(), SaveBookingUseCase],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './service-detail.component.html',
  styleUrl: './service-detail.component.scss',
})
export class ServiceDetailComponent {
  isLoading = signal(false);
  bookingStore = inject(BookingStore);
  route = inject(ActivatedRoute);
  router = inject(Router);
  dialog = inject(MatDialog);
  private _snackbar = inject(MatSnackBar);
  service = this.bookingStore.selectedServiceId;
  private readonly saveBookingUseCase = inject(SaveBookingUseCase);
  private readonly getBookingsByServiceUseCase = inject(
    GetBookingsByServiceUseCase
  );
  private loadSubscription?: Subscription;
  displayedColumns: string[] = [
    'employeeName',
    'serviceDetailName',
    'startDateTime',
    'endDateTime',
  ];
  dataSource = new MatTableDataSource<BookingRow>([]);

  constructor() {
    effect(() => {
      const id = this.service()?.id;
      untracked(() => this.loadBookings(id));
    });

    inject(DestroyRef).onDestroy(() => this.loadSubscription?.unsubscribe());
  }

  loadBookings(id = this.service()?.id) {
    // Drop any load still in flight so an older response can't overwrite a newer one.
    this.loadSubscription?.unsubscribe();

    if (!id) {
      this.dataSource.data = [];
      return;
    }

    this.isLoading.set(true);
    this.loadSubscription = this.getBookingsByServiceUseCase
      .execute(id)
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (bookings) => (this.dataSource.data = bookings),
        error: (err) =>
          this._snackbar.open(this.toUserMessage(err), '', {
            duration: 3000,
          }),
      });
  }

  openModal(detail: any) {
    const dialogRef = this.dialog.open(BookingModal, {
      width: '400px',
      data: detail,
    });

    const modal = dialogRef.componentInstance;

    modal.saveRequested$
      .pipe(
        exhaustMap((dto) => {
          modal.setLoading(true);
          return this.saveBookingUseCase.execute(dto).pipe(
            catchError((err) => {
              modal.setError(this.toUserMessage(err));
              return EMPTY;
            }),
            finalize(() => modal.setLoading(false))
          );
        }),
        takeUntil(dialogRef.afterClosed())
      )
      .subscribe((saved) => dialogRef.close(saved));

    dialogRef
      .afterClosed()
      .pipe(filter(Boolean))
      .subscribe(() => {
        this._snackbar.open('Booking saved successfully!', '', {
          duration: 3000,
        });
        this.loadBookings();
      });
  }

  private toUserMessage(err: any): string {
    return err?.error?.message ?? 'Error. Please try again.';
  }
}
