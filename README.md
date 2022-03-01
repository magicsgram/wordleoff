**I intend to keep this free and ad-free (and open source) as long as my time and budget allow to maintain the server. It's running on a low-tier server, So I apologize for any service interruptions.**
***

WORDLEOFF is a project hosted on
https://www.wordleoff.com

with heavy inspiration from
- Wordle at https://www.powerlanguage.co.uk/
- Social elements of Among Us' discussion mechanics  https://www.innersloth.com/games/among-us/

Unlike some other Wordle variants you see online, this is NOT a 'race to the answer' type of game.

All players must submit their guesses before you can submit another one, keeping everyone on the same pace and no one gets too far too fast. So yes, you can play this w/ your grandparent too. I wanted to focus more on the social mechanics, not how speedy you are at guessing answers.

Get on Discord/Zoom/Skype/Google Meets w/ your friends and family and invite them by sending them a link to your session (you can share a link by clicking on the session ID on top)
- Everyone has to submit a guess until you can move on to a next guess. Pressure your peers to hurry up :)
- Once you get the answer, you enter a 'God Mode' and get to see everyone else's guess. Tell them you can see their guesses and give them even more pressure :) :)
- Up to 16 people can join a session simultaneously. Although possible, I do not recommend playing this on mobile if you have more than 4 people.
- Once the game is over (either everyone got the answer or failed), you'll be prompted a link to see the definition on dictionary.com . I had to add this link because I got so frustrated w/ some answers on Wordle in the past couple weeks.

Enjoy and let me know if you have any questions, feedbacks, etc.

***Some thoughts if you ever played a game called 'Among Us'.***

I freaking love the voting mechanics in 'Among Us', as there's so much tension and pressure to 'vote' on Imposters. Also, after the imposters kill you and you know who they are, it gets even more interesting because you get to spectate how other people struggle in search of them.

WORDLEOFF is loosely around this idea and it's much more fun if you're on voice chats w/ your peers.


***
Changelog

#### Feb 17th, 2022
My current top priority is to save all sessions into database, so that you can play the game when the server experience short outage and come back alive. (and when you lose your connection to the internet and reconnect). I originally intended this to be a proof-of-concept. So, I just saved everything onto memory. But since people showed their love, I'm gonna build a db integration, so that you don't lose your progress anymore. Your session will still be destroyed if there's no activities for X amount of time (I think 1 hour?). I'll let you know when it's released. Thank you all for your support.

#### Feb 28th, 2022
1. Restoring connection

As long as you keep the tab open on your browser, your lost connection can be restored. The page will either reconnect to the server by itself, or if you see any error messages at the bottom, simply click on 'reload'. I have a plan to optimize this further.

2. Persist session progress

When the server experience short downtime(the server gets rebooted as least once every 24 hours), your session progress is stored in database, meaning as soon as the server comes back online, you'll be able to resume from where you left off. (Obviously, your connection needs to be restored as I described in #1)

3. High contrast mode and tile animations

These were available in public for a while now, but there's a 'HC(High Contrast)' switch at bottom-right corner, and the tiles show various animations.

4. Joining mid-session in progress

You no longer have to wait until the current round is complete and reset in order to join the game. However, if you join in mid-session, you have to catch up w/ every else's guesses. (i.e. if people already made 3 guesses and you join the session, you have to submit 3 guesses first before they can submit their guesses again.)
