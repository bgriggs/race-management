import { Component } from '@angular/core';

@Component({
  selector: 'rm-local-dashboard',
  standalone: true,
  template: `
    <section class="dashboard-shell">
      <h1>Dashboard</h1>
      <p>Live race and vehicle summary data will appear here.</p>
    </section>
  `,
  styles: [
    `
      .dashboard-shell {
        background: #ffffff;
        border: 1px solid #d9e1ec;
        border-radius: 0.6rem;
        padding: 1rem;
      }

      h1 {
        margin: 0 0 0.5rem;
        font-size: 1.3rem;
      }

      p {
        margin: 0;
        color: #44556a;
      }
    `,
  ],
})
export class DashboardComponent {}
