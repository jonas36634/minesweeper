"""Run against a served release with Playwright and Chromium."""
import argparse
import os
import shutil
import re
import time
from playwright.sync_api import sync_playwright, expect

parser = argparse.ArgumentParser()
parser.add_argument('url', nargs='?', default='http://localhost:5173')
args = parser.parse_args()

def saved(page):
    return page.evaluate("JSON.parse(localStorage.getItem('kernport.game1'))")

def cell(page, index):
    return page.locator(f'[data-cell="{index}"]')

def prepare(page, level, mode):
    page.locator('#mode').select_option(mode)
    if mode != 'Daily':
        page.locator('#level').select_option(str(level))
    page.get_by_role('button', name='Neues Spiel', exact=True).click()
    if page.locator('dialog').count():
        page.get_by_role('button', name='Neu starten', exact=True).click()

def layout(game):
    w,h,m = (game['Size'][k] for k in ('Width','Height','Mines'))
    first = next(move['Cell'] for move in game['Moves'] if not move['Flag'])
    pool = [n for n in range(w*h) if abs(n%w-first%w)>1 or abs(n//w-first//w)>1]
    seed=game['Seed']
    for j in range(m):
        seed=(seed*1664525+1013904223)&0xffffffff
        k=j+(seed*(len(pool)-j)>>32)
        pool[j],pool[k]=pool[k],pool[j]
    return set(pool[:m])

def start(page):
    page.goto(args.url)
    page.wait_for_function("window.kp?.boardKey != null")
    expect(page.locator('.cell').first).to_be_visible()

with sync_playwright() as p:
    browser=p.chromium.launch(executable_path=os.environ.get('CHROMIUM') or shutil.which('chromium'),args=['--no-sandbox'])
    desktop=browser.new_page(viewport={'width':1280,'height':900})
    errors=[]
    desktop.on('pageerror',lambda e:errors.append(str(e)))
    start(desktop)
    expect(desktop.locator('.cell')).to_have_count(81)
    cell(desktop,0).focus()
    desktop.keyboard.press('ArrowRight')
    expect(cell(desktop,1)).to_be_focused()
    desktop.keyboard.press('f')
    expect(cell(desktop,1)).to_have_class(re.compile(r' flag'))
    desktop.keyboard.press('f')
    desktop.keyboard.press('Enter')
    expect(cell(desktop,1)).to_have_attribute('aria-label','Zeile 1, Spalte 2, leer')
    game=saved(desktop)
    opened=desktop.locator('.cell.open').count()
    desktop.reload()
    expect(desktop.locator('.cell.open')).to_have_count(opened)
    desktop.get_by_role('button',name='Neues Spiel',exact=True).click()
    desktop.get_by_role('button',name='Weiterspielen').click()
    assert saved(desktop)['Id']==game['Id']
    field=layout(game)
    for index in range(81):
        if index not in field and 'closed' in cell(desktop,index).get_attribute('class'):
            cell(desktop,index).click()
    expect(desktop.locator('.live')).to_have_text('Gewonnen')
    desktop.reload()
    expect(desktop.locator('.live')).to_have_text('Gewonnen')
    desktop.get_by_role('button',name='Statistik',exact=True).click()
    expect(desktop.locator('dialog')).to_contain_text('1 / 1')
    desktop.get_by_text('Schließen',exact=True).click()
    prepare(desktop,0,'Logic')
    cell(desktop,40).click()
    expect(desktop.locator('.live')).to_have_text('Logisch geprüft',timeout=8000)
    for _ in range(81):
        if desktop.locator('.live').inner_text()=='Gewonnen':break
        desktop.get_by_role('button',name='Hinweis',exact=True).click()
        target=desktop.locator('.cell.target')
        expect(target).to_have_count(1)
        message=desktop.locator('.message').inner_text()
        assert 'Zeile' in message and ('sicher' in message or 'Mine' in message)
        target.click(button='right' if ': Mine.' in message else 'left')
    expect(desktop.locator('.live')).to_have_text('Gewonnen')
    desktop.get_by_role('button',name='Statistik',exact=True).click()
    expect(desktop.locator('dialog')).to_contain_text('mit Hinweis')
    desktop.keyboard.press('Escape')
    prepare(desktop,2,'Logic')
    timings=[]
    for attempt in range(5):
        t=time.perf_counter()
        cell(desktop,255).click()
        desktop.wait_for_function("document.querySelector('.live').textContent!=='Prüfung läuft'")
        timings.append(round(time.perf_counter()-t,2))
        if desktop.locator('.live').inner_text()=='Logisch geprüft':break
    print('Experten-Logik im Browser, Sekunden pro Versuch:',timings,desktop.locator('.live').inner_text(),flush=True)
    prepare(desktop,1,'Daily')
    cell(desktop,0).click()
    assert saved(desktop)['Moves']==[]
    cell(desktop,136).click()
    daily=saved(desktop)
    daily_open=desktop.locator('.cell.open').count()
    cell(desktop,min(layout(daily))).click()
    expect(desktop.locator('.live')).to_have_text('Mine getroffen')
    desktop.reload()
    expect(desktop.locator('.boom')).to_have_count(1)
    desktop.get_by_text('Noch einmal spielen').click()
    expect(desktop.locator('.cell.open')).to_have_count(0)
    mobile=browser.new_context(viewport={'width':375,'height':812},is_mobile=True,has_touch=True)
    phone=mobile.new_page()
    phone.on('pageerror',lambda e:errors.append(str(e)))
    start(phone)
    prepare(phone,1,'Daily')
    cell(phone,136).tap()
    assert saved(phone)['Seed']==daily['Seed'] and phone.locator('.cell.open').count()==daily_open
    expect(phone.locator('.scroller')).to_be_visible()
    before=saved(phone)['Moves']
    phone.locator('.scroller').evaluate('(s)=>s.scrollLeft=0')
    phone.locator('.scroller').evaluate("s=>s.scrollIntoView({block:'center'})")
    box=phone.locator('.scroller').bounding_box()
    y=box['y']+min(80,box['height']/2)
    cdp=mobile.new_cdp_session(phone)
    def touch(kind,x=None):
        points=[] if x is None else [{'x':x,'y':y}]
        cdp.send('Input.dispatchTouchEvent',{'type':kind,'touchPoints':points})
    touch('touchStart',300)
    for x in range(280,70,-20):touch('touchMove',x)
    touch('touchEnd')
    phone.wait_for_timeout(250)
    assert phone.locator('.scroller').evaluate('s=>s.scrollLeft')>0 and saved(phone)['Moves']==before
    assert phone.evaluate('document.documentElement.scrollWidth<=innerWidth')
    phone.get_by_role('button',name='Flagge',exact=True).tap()
    target=phone.locator('.cell.closed').first
    target.tap()
    expect(target).to_have_class(re.compile(r' flag'))
    phone.reload()
    expect(phone.locator('.cell.flag')).to_have_count(1)
    phone.get_by_role('button',name='Farbschema wechseln').tap()
    expect(phone.locator('html')).to_have_attribute('data-theme','dark')
    phone.add_init_script("localStorage.setItem('kernport.game1','{broken')")
    phone.reload()
    expect(phone.locator('.message')).to_contain_text('alte Spielstand')
    cell(phone,0).tap()
    expect(phone.locator('.live')).to_have_text('Klassisch')
    assert not errors,errors
    print('Bestanden: Tastatur, Neustartschutz, klassischer Sieg, Logiksieg mit Hinweisen, gespeicherte Endbretter, Tagesaufgabe, Verlust, Touch-Scroll, Flaggen, Farbschema und beschädigter Spielstand.')
