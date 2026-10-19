import urllib.request, json, io, base64
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
from PIL import Image, ImageDraw

ROOT=Path(__file__).parent
GAMES=[
(211820,'星界边境','太空科幻','像素太空题材；观察一个符号如何承载整个世界。'),
(1501610,'新星岛','太空科幻','清爽科幻配色；考虑把磁铁做成有厚度的小型道具。'),
(383980,'以太之战','能量对比','像素轮廓与鲜明色块；参考红蓝两极的色彩区分。'),
(311690,'挺进地牢','像素主体','紧凑主体与像素轮廓；参考缩小后的识别能力。'),
(606150,'夜勤人','像素主体','道具化识别；考虑用磁铁本身替代角色作为主视觉。'),
(751780,'浮岛物语','像素主体','简化造型；减少磁铁上的纹理和无关装饰。'),
(413150,'星露谷物语','像素主体','亲和的像素物件；参考明暗块如何表现厚度。'),
(105600,'泰拉瑞亚','像素主体','单一物件轮廓；思考只留下最有辨识度的形状。'),
(504230,'蔚蓝','像素主体','清晰的视觉标识；参考小尺寸图标的主体取舍。'),
(250760,'铲子骑士：无主珍宝','像素主体','高对比主体；参考蓝色物件在暗部中的轮廓。'),
(257850,'光明旅者','能量对比','冷暖对比与简洁符号；适合参考红蓝磁极的冲突感。'),
(460950,'武士 零','能量对比','霓虹与锐利轮廓；参考中央连接能量的节奏。'),
(588650,'死亡细胞','能量对比','强烈色彩焦点；参考用亮色引导视线。'),
(332200,'公理边缘','太空科幻','复古科幻气质；参考深色背景上的明亮符号。'),
(212680,'超越光速','太空科幻','太空题材的精简表达；参考科幻标识与背景的分工。'),
(590380,'陷阵之志','太空科幻','小型科幻物件与机械感；参考磁铁的硬朗体积。'),
(736260,'Baba Is You','解谜符号','极简像素识别；参考如何让核心概念一眼可读。'),
(1260520,'帕特里克的无穷箱子','解谜符号','非像素方向补充；参考两个形体之间的空间关系。'),
(375820,'人力资源机器','解谜符号','非像素方向补充；参考一个简洁符号如何代表解谜玩法。'),
(1318690,'异形工厂','解谜符号','非像素方向补充；参考简洁几何标识与双色形体组合。'),
]

def get(url):
    return urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'Mozilla/5.0'}),timeout=45).read()
def fetch(item):
    app,cn,tag,note=item
    common=json.loads(get(f'https://api.steamcmd.net/v1/info/{app}'))['data'][str(app)]['common']
    urls=[]
    if common.get('clienticon'): urls.append(f"https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/{app}/{common['clienticon']}.ico")
    if common.get('icon'): urls.append(f"https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/{app}/{common['icon']}.jpg")
    for url in urls:
        try:
            im=Image.open(io.BytesIO(get(url))).convert('RGBA')
            buf=io.BytesIO(); im.save(buf,format='PNG')
            (ROOT/f'{app}.png').write_bytes(buf.getvalue())
            result=dict(id=app,cn=cn,name=common['name'],tag=tag,note=note,source=url,width=im.width,height=im.height,img='data:image/png;base64,'+base64.b64encode(buf.getvalue()).decode())
            print(app,common['name'],im.size,flush=True)
            return result
        except Exception as exc: print('retry',app,str(exc),flush=True)
    raise RuntimeError(f'No icon: {app}')

if __name__=='__main__':
    with ThreadPoolExecutor(max_workers=5) as pool: data=list(pool.map(fetch,GAMES))
    (ROOT/'icons.json').write_text(json.dumps(data,ensure_ascii=False),encoding='utf-8')
    sheet=Image.new('RGB',(1000,800),'#171d2d'); draw=ImageDraw.Draw(sheet)
    for n,g in enumerate(data):
        im=Image.open(ROOT/f"{g['id']}.png").convert('RGBA'); im.thumbnail((144,144))
        x=(n%5)*200;y=(n//5)*200
        sheet.paste(im,(x+28,y+8),im); draw.text((x+8,y+160),f"{n+1}. {g['name'][:25]}",fill='white')
    sheet.save(ROOT/'contact-sheet.jpg')
