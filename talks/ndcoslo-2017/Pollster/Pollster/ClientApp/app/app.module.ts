import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';
import { UniversalModule } from 'angular2-universal';
import { AppComponent } from './components/app/app.component'
import { NavMenuComponent } from './components/navmenu/navmenu.component';
import { PollsComponent } from './components/polls/polls.component';
import { keyValueFilterPipe } from './components/utils/custom.pipes';

@NgModule({
    bootstrap: [ AppComponent ],
    declarations: [
        AppComponent,
        NavMenuComponent,
        PollsComponent,
        keyValueFilterPipe
    ],
    imports: [
        UniversalModule, // Must be first import. This automatically imports BrowserModule, HttpModule, and JsonpModule too.
        RouterModule.forRoot([
            { path: '', redirectTo: 'polls', pathMatch: 'full' },
            { path: 'polls', component: PollsComponent },
            { path: '**', redirectTo: 'polls' }
        ])
    ]
})
export class AppModule {
}
