import test from 'node:test';
import assert from 'node:assert/strict';
import {isBookingIntent} from '../../wwwroot/assets/js/core/bookingIntent.js';

const positives=['Записаться на приём','Book an appointment','Schedule a visit','Prendre rendez-vous','Κλείστε ραντεβού','حجز موعد'];
for(const value of positives)test(`booking intent: ${value}`,()=>assert.equal(isBookingIntent(value),true));
test('ordinary dental question is not booking intent',()=>assert.equal(isBookingIntent('How much is whitening?'),false));
