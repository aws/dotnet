import { Component } from '@angular/core';
import { Http } from '@angular/http';

@Component({
    selector: 'polls',
    templateUrl: './polls.component.html'
})
export class PollsComponent {
    title: string = 'Pollster Feed ';

    private _http: Http;

    public polls: Polls[];

    constructor(http: Http) {
        this._http = http;

        this._http.get('./api/pollfeeds/').subscribe(result => {
            this.polls = result.json() as Polls[];
        });
    }

    public submitVote(pollId: string) {
        var checkedOption = document.querySelector('input[name="' + pollId + '"]:checked') as HTMLInputElement;

        var selectedPoll = null;
        for (var index = 0; index < this.polls.length; ++index) {
            if (this.polls[index].id == pollId) {
                selectedPoll = this.polls[index];
                break;
            }
        }

        if (checkedOption === null) {
            alert('You must select a choice before voting')
            return;
        }

        this._http.put('./api/pollvoter/' + pollId + '/' + checkedOption.value, null).subscribe(result => {

            var button = document.querySelector("button[id='" + pollId + "']") as HTMLInputElement;
            button.disabled = true;
            button.classList.add("voteComplete");

            var parent = document.querySelector("ul[id='" + pollId + "']") as HTMLElement;

            while (parent.firstChild) {
                parent.removeChild(parent.firstChild);
            }


            var currentVotes = result.json();
            for (var optionId in currentVotes) {
                var message = selectedPoll.options[optionId].text + ": " + currentVotes[optionId];
                var elem = document.createElement("li");
                elem.classList.add("polloption");
                elem.innerHTML = message;
                parent.appendChild(elem);
            }
        });
    }
}

interface Polls {
    id: string;
    title: string;
    question: string;
    options: any;
}
