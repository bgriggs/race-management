import { Component } from '@angular/core';
import { APP_VERSION } from '../../version';

@Component({
  selector: 'rm-version-footer',
  standalone: true,
  templateUrl: './version-footer.component.html',
  styleUrl: './version-footer.component.css',
})
export class VersionFooterComponent {
  protected readonly version = APP_VERSION;
}
