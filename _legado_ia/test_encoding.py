s = 'PROTEÇÃƒO'
print(s)
try:
    print(s.encode('cp1252').decode('utf-8'))
except Exception as e:
    print('ERR', e)
