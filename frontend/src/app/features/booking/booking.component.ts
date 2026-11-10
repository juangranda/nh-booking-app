import { Component, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  ActivatedRoute,
  NavigationEnd,
  ParamMap,
  Router,
  RouterOutlet,
} from '@angular/router';
import { OfficeRepository } from './domain/repositories/office.repository';
import { OfficeImplRepository } from './data/repositories/office-impl.repository';
import { GetServicesUseCase } from './domain/use-cases/get-services.usecase';
import { GetWorkingPlacesUseCase } from './domain/use-cases/get-working-places.usecase';
import { ServiceRepository } from './domain/repositories/service.repository';
import { ServiceImplRepository } from './data/repositories/service-impl.repository';
import { BookingStore } from './presentation/state/booking.store';
import { combineLatest, filter, map, shareReplay } from 'rxjs';
import { MatSnackBar } from '@angular/material/snack-bar';
import { SaveBookingUseCase } from './domain/use-cases/save-booking.usecase';
import { BookingRepository } from './domain/repositories/booking.repository';
import { BookingImplRepository } from './data/repositories/booking-impl.repository';
import { GetBookingsByServiceUseCase } from './domain/use-cases/get-bookings-by-service.usecase';

@Component({
  selector: 'app-booking',
  imports: [RouterOutlet],
  providers: [
    {
      provide: OfficeRepository,
      useClass: OfficeImplRepository,
    },
    GetWorkingPlacesUseCase,
    {
      provide: ServiceRepository,
      useClass: ServiceImplRepository,
    },
    GetServicesUseCase,
    {
      provide: BookingRepository,
      useClass: BookingImplRepository,
    },
    SaveBookingUseCase,
    GetBookingsByServiceUseCase,
    BookingStore,
  ],
  template: `<router-outlet></router-outlet>`,
})
export class BookingComponent {
  bookingStore = inject(BookingStore);
  private _snackbar = inject(MatSnackBar);
  route = inject(ActivatedRoute);
  router = inject(Router);

  private readonly params$ = this.router.events.pipe(
    filter((e) => e instanceof NavigationEnd),
    map(() => this.deepestParams()),
    shareReplay({ bufferSize: 1, refCount: true })
  );

  constructor() {
    this.bookingStore.onLoad();

    combineLatest([
      this.bookingStore.workingPlaces$.pipe(filter((list) => list.length > 0)),
      this.params$,
    ])
      .pipe(takeUntilDestroyed())
      .subscribe(([, params]) => {
        const id = params.get('id');
        if (id) {
          this.select(() => this.bookingStore.setSelectedOfficeId(Number(id)));
        }
      });

    combineLatest([this.bookingStore.services$, this.params$])
      .pipe(takeUntilDestroyed())
      .subscribe(([services, params]) => {
        const id = Number(params.get('id'));
        const sId = params.get('sId');
        // Wait until the services for the office in the URL have loaded.
        if (!sId || services.length === 0 || services[0].officeId !== id) {
          return;
        }
        this.select(() => this.bookingStore.setServiceId(Number(sId)));
      });
  }

  private deepestParams(): ParamMap {
    let route = this.route;
    while (route.firstChild) {
      route = route.firstChild;
    }
    return route.snapshot.paramMap;
  }

  private select(action: () => void) {
    try {
      action();
    } catch (err: any) {
      this._snackbar.open(err.message, '', { duration: 3000 });
      this.router.navigate(['booking', 'working-place']);
    }
  }
}
