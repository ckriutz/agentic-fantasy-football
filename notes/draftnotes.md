# Draft Notes

## Problems with some providers
deepseek/deepseek-v4-flash model sometimes fails bootstrapping, when I returned to it, I would ocassionally get the error: `Service request failed. Status: 402 (Payment Required)` which isn't the case with other models. For now, it's too unreliable, so I'm falling back on the deepseek/deepseek-v3.2 model, which works.

I was curious about using arcee-ai/trinity-large-thinking, however it doesn't do well with toold, and didnt select a player at all. This makes me think this model is not qualified to play, so it got booted.

I wanted to use the ibm-granite/granite-4.1-8b model, which I've heard good things about, but it's really bad at fantasy football. I might bring it back in after testing, but for now it got the boot.

Also, amazon/nova-2-lite-v1 is not doing well with tool calling, so it got the boot also.

nvidia/nemotron-3-super-120b-a12b is one I *really* want to work but it's constantly on thin ice. In a test draft, it failed twice at drafting a player. Mostly related to tool-calling. This may be a good reason to update tool definitions and see what can be improved here.


## Observations
Good searches like:
best fantasy football player to draft in round 2 pick 11 10 team full PPR league 2025 season after taking RB early
Derrick Henry fantasy outlook 2025 PPR
Ashton Jeanty fantasy ranking 2025 draft advice