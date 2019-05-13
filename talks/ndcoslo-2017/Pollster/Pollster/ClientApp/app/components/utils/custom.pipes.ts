import { Component } from '@angular/core';
import { Pipe } from '@angular/core';

@Pipe({
    name: 'keyValueFilter'
})
export class keyValueFilterPipe {
    transform(value: any, args: any[] = null): any {

        return Object.keys(value).map(function (key) {
            let pair = {};
            let k = 'key';
            let v = 'value'


            pair[k] = key;
            pair[v] = value[key];

            return pair;
        });
    }

}
