import { Component, input, output } from '@angular/core';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';

@Component({
  selector: 'lib-can-bus-config',
  imports: [MatSlideToggleModule],
  templateUrl: './can-bus-config.html',
  styleUrl: './can-bus-config.css',
})
export class CanBusConfig {
  readonly canBus1Enabled = input(false);
  readonly canBus2Enabled = input(false);
  readonly canBus1EnabledChange = output<boolean>();
  readonly canBus2EnabledChange = output<boolean>();

  onCanBus1Toggle(checked: boolean): void {
    this.canBus1EnabledChange.emit(checked);
  }

  onCanBus2Toggle(checked: boolean): void {
    this.canBus2EnabledChange.emit(checked);
  }
}
