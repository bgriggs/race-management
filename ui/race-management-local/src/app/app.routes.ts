import { Routes } from '@angular/router';
import { CarConfigurationComponent } from '../../../shared-ui/src/lib/configuration/car-configuration/car-configuration.component';
import { DashboardComponent } from './dashboard.component';

export const routes: Routes = [
	{
		path: '',
		component: DashboardComponent,
	},
	{
		path: 'dashboard',
		component: DashboardComponent,
	},
	{
		path: 'configuration',
		component: CarConfigurationComponent,
	},
	{
		path: '**',
		redirectTo: '',
	},
];
