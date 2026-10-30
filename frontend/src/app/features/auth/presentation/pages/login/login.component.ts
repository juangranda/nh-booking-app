import { Component, inject, signal } from '@angular/core';

import { ErrorStateMatcher } from '@angular/material/core';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { HttpErrorResponse } from '@angular/common/http';
import { LoginUseCase } from '../../../domain/use-cases/login.usecase';
import { firstValueFrom } from 'rxjs';
import { Router } from '@angular/router';
import {
  FormControl,
  FormGroup,
  FormGroupDirective,
  NgForm,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';

export class MyErrorStateMatcher implements ErrorStateMatcher {
  isErrorState(
    control: FormControl | null,
    form: FormGroupDirective | NgForm | null
  ): boolean {
    const isSubmitted = form && form.submitted;
    return !!(
      control &&
      control.invalid &&
      (control.dirty || control.touched || isSubmitted)
    );
  }
}

@Component({
  selector: 'app-login',
  imports: [MatInputModule, ReactiveFormsModule, MatButtonModule, MatSnackBarModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  loginForm = new FormGroup({
    username: new FormControl<string | null>(null, [Validators.required]),
    password: new FormControl<string | null>(null, [Validators.required]),
  });
  matcher = new MyErrorStateMatcher();

  readonly loginUseCase = inject(LoginUseCase);

  isLoading = signal(false);
  private router = inject(Router);
  private snackBar = inject(MatSnackBar);

  async onSubmit() {
    if (this.loginForm.invalid || this.isLoading()) return;

    const username = this.loginForm.get('username')?.value;
    const password = this.loginForm.get('password')?.value;
    try {
      this.isLoading.set(true);
      // The repository stores the session, so there is nothing to save here.
      await firstValueFrom(
        this.loginUseCase.execute({
          username: username!,
          password: password!,
        })
      );
      this.loginForm.reset();
      this.router.navigate(['/booking']);
    } catch (error) {
      this.snackBar.open(this.toUserMessage(error), 'Close', { duration: 5000 });
    } finally {
      this.isLoading.set(false);
    }
  }

  private toUserMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0) return 'Cannot reach the server. Please try again later.';
      if (error.status === 401) return error.error?.message ?? 'Invalid email or password.';
      if (typeof error.error?.message === 'string') return error.error.message;
    }
    return 'Login failed. Please try again.';
  }
}
