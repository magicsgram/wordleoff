***New York Times Starts Sending DMCA Takedowns to Wordle Clones https://www.theverge.com/2024/3/8/24094234/ny-times-wordle-clones-files-dmca-copyright-takedowns-knockoffs. I haven't received one yet, but if they target Wordleoff, it might have to go offline.***

***I haven't spoken about this publicly, but Wordleoff still has thousands of daily players worldwide (especially you high schoolers!). At its peak, concurrent players reached around 1,500. As of March 8, 2024, 1,899,848 players have played a total of 3,858,296 rounds across 926,165 sessions. That's a combined playtime of 1,464,522,427 seconds, or roughly 46.44 years!***

***A big thank you to all the players who've kept Wordleoff going strong! I've kept the game ad-free and shouldered the server costs myself. I intend to for as long as possible or the game ceases to exist.***

***Have a GREAT day!***

***March 8th, 2024.***

***Kyle***


**PS. Back in 2010, I wrote my first academic paper on "WORDLE," which wasn't a game at that time but a word cloud visualization tool. https://ieeexplore.ieee.org/abstract/document/5613458?casa_token=EpHPAw7AWHAAAAAA:vHJiK00QqeWgLwNbJDKfjVgQrrOntWeq61StjMxZ-d-uNvjEWSV06e5Igeo_GVTcoI9pdinV2Q **
**I was thrilled to see the word resurface in pop culture, but the New York Times now owns the trademark, making it their intellectual property. This is a sad day for the community.**

***
**This project will be completely free of ads and open source as long as I can maintain it. Please note that it's running on limited resources, so if you encounter any service interruptions, simply refresh the page or try again later. Thank you for your understanding!**
***

WORDLEOFF is a project hosted on
https://www.wordleoff.com

with heavy inspiration from
- Wordle at https://www.powerlanguage.co.uk/
- Social elements of Among Us' discussion mechanics  https://www.innersloth.com/games/among-us/

Unlike some other Wordle variants you see online, this is NOT a 'race to the answer' type of game.

All players must submit their guesses before you can submit another one, keeping everyone on the same pace and no one gets too far too fast. So yes, you can play this w/ your grandparent too. I wanted to focus more on the social mechanics, not how speedy you are at guessing answers.

Gather your friends and family on Discord, Zoom, Skype, or Google Meets! Send them a link to your session by clicking the session ID at the top.
- Everyone has to submit a guess until you can move on to a next guess. Pressure your peers to hurry up :)
- Once you get the answer, you enter a 'God Mode' and get to see everyone else's guess. Tell them you can see their guesses and give them even more pressure :) :)
- Up to 30 people can join a session simultaneously, but mobile gameplay is best for groups of 4 or less.
- Once the game is over (either everyone got the answer or failed), you'll be prompted a link to see the definition on dictionary.com . I had to add this link because I got so frustrated w/ some answers on Wordle in the past couple weeks.

Enjoy and let me know if you have any questions, feedbacks, etc.

***Some thoughts if you ever played a game called 'Among Us'.***

I absolutely adore the voting dynamics in "Among Us!" The suspense and pressure to identify the Imposters makes for incredibly thrilling gameplay. Even after being eliminated, watching the crewmates navigate the hunt adds another layer of intrigue.

WORDLEOFF takes inspiration from this concept, but with a twist! Playing with friends on voice chat amps up the excitement tenfold, creating a truly unique social experience.


***
Changelog

#### Feb 17th, 2022
My top priority right now is to save all game sessions to a database. This means you won't lose your progress even if the server has a short outage or you disconnect and reconnect to the internet. I know I originally planned this as a simple proof-of-concept, storing everything in memory, but thanks to all your amazing support, I'm taking it a step further with database integration. Remember, sessions will still be inactive after X amount of time (probably around 1 hour) with no activity. I'll let you know the exact timing when the feature is released. Thank you again for your support! It's your love for the game that motivates me to keep improving it.

#### Feb 28th, 2022
1. Restoring connection

As long as you keep the tab open on your browser, your lost connection can be restored. The page will either reconnect to the server by itself, or if you see any error messages at the bottom, simply click on 'reload'. I have a plan to optimize this further.

2. Persist session progress

When the server experience short downtime(the server gets rebooted as least once every 24 hours), your session progress is stored in database, meaning as soon as the server comes back online, you'll be able to resume from where you left off. (Obviously, your connection needs to be restored as I described in #1)

3. High contrast mode and tile animations

We've had some hidden features waiting for you to discover! Check out the High Contrast(HC) mode switch in the bottom right corner for a more accessible reading experience. Plus, don't miss the fun tile animations that add a touch of magic to your gameplay.

4. Joining mid-session in progress

No need to wait for a new round anymore! You can jump into the fun anytime. Just be aware that if you join an ongoing session, you'll need to catch up with everyone else's guesses before they can make their next move. So, if they've already guessed 3 times, you'll need to submit 3 guesses of your own first. Think of it as warming up before joining the wordplay race!

#### May 5th, 2022

Adding Streamer Mode

#### Aug 25th, 2022

Adding Spectator Mode
