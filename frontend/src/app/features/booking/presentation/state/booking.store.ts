import { inject, Injectable, signal } from '@angular/core';
import Office from '../../domain/models/office.model';
import { BehaviorSubject, firstValueFrom } from 'rxjs';
import ServiceModel from '../../domain/models/service.model';
import { GetWorkingPlacesUseCase } from '../../domain/use-cases/get-working-places.usecase';
import { GetServicesUseCase } from '../../domain/use-cases/get-services.usecase';

@Injectable()
export class BookingStore {
  private readonly getWorkingPlacesUseCase = inject(GetWorkingPlacesUseCase);
  private readonly getServicesUseCase = inject(GetServicesUseCase);

  private readonly _workingPlaces = new BehaviorSubject<Office[]>([]);
  readonly workingPlaces$ = this._workingPlaces.asObservable();
  readonly selectedOfficeId = signal<Office | null>(null);

  private readonly _services = new BehaviorSubject<ServiceModel[]>([]);
  readonly services$ = this._services.asObservable();
  readonly selectedServiceId = signal<ServiceModel | null>(null);

  //TODO: Nothing uses this now. Need to implement a spinner using this flag.
  isLoadingServices = signal(false);

  private servicesRequest = 0;

  getServices() {
    return this._services.value;
  }

  setServices(services: ServiceModel[]) {
    this._services.next(services);
  }

  getSelectedWorkingPlace() {
    return this.selectedOfficeId();
  }

  setServiceId(sId: number) {
    const service = this._services.value.find((s) => s.id === sId);
    if (!service) {
      throw Error('The id cannot be found');
    }
    if (this.selectedServiceId()?.id !== sId) {
      this.selectedServiceId.set(service);
    }
  }

  setSelectedOfficeId(id: number) {
    if (this.selectedOfficeId()?.id === id) {
      return;
    }
    const place = this._workingPlaces.value.find((place) => place.id === id);
    if (!place) {
      throw Error('The id cannot be found');
    }
    this.selectedOfficeId.set(place);
    this.selectedServiceId.set(null);
    this.loadServices(id);
  }

  setWorkingPlaces(places: Office[]) {
    this._workingPlaces.next(places);
  }

  async onLoad() {
    const wPlaces = await firstValueFrom(
      this.getWorkingPlacesUseCase.execute()
    );
    this.setWorkingPlaces(wPlaces);
  }

  async loadServices(officeId: number) {
    const request = ++this.servicesRequest;
    const isLatest = () => request === this.servicesRequest;

    this.setServices([]);
    this.isLoadingServices.set(true);
    try {
      const services = await firstValueFrom(
        this.getServicesUseCase.execute(officeId)
      );
      if (isLatest()) this.setServices(services);
    } catch {
      if (isLatest()) this.setServices([]);
    } finally {
      if (isLatest()) this.isLoadingServices.set(false);
    }
  }
}
