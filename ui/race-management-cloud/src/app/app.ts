import { Component } from '@angular/core';
import { CarConfigurationComponent } from '../../../shared-ui/src/lib/configuration/car-configuration/car-configuration.component';

@Component({
  selector: 'app-root',
  imports: [CarConfigurationComponent],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {}
