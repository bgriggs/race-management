import { Component } from '@angular/core';
import { SharedBannerComponent } from '../../../shared-ui/src/lib/shared-banner.component';

@Component({
  selector: 'app-root',
  imports: [SharedBannerComponent],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {}
