"""Isolated CI acceptance. Never accepts an arbitrary target or existing database.

Creates two NEW er84_* schemas on the disposable GitHub service, retains all
synthetic data for artifacts, and exercises real trigger DML and DDL recovery.
"""
import copy
import datetime
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time
import uuid
from runtime_diagnostic import RuntimeDiagnostic
from mysql84_release import Mysql, Runner, ROOT, identity, install, load_contract, verify_schema, fingerprint, literal, require, sha


def value(item):
    if item is None:
        return 'NULL'
    if isinstance(item, (int, float)):
        return str(item)
    return literal(json.dumps(item) if isinstance(item, (dict, list)) else item)


def insert(table, row):
    return 'INSERT INTO `' + table + '` (' + ','.join('`' + k + '`' for k in row) + ') VALUES (' + ','.join(value(v) for v in row.values()) + ')'


class Probes:
    def __init__(self, db, config):
        self.db, self.config, self.coverage = db, config, []
        self.ids = {k: str(uuid.uuid4()) for k in ['tenant', 'other', 'user', 'targetUser', 'member', 'target', 'invite', 'event', 'batch', 'plan', 'path', 'recovery', 'recoveryEvent', 'approvalEvent']}
        self.now = datetime.datetime.now(datetime.timezone.utc).replace(tzinfo=None)
        self.created = self.now.strftime('%Y-%m-%d %H:%M:%S.%f')
        self.expiry = (self.now + datetime.timedelta(hours=2)).strftime('%Y-%m-%d %H:%M:%S.%f')
        self.hash = sha(str(uuid.uuid4()))
        self.contract = load_contract()
        self.failures, self.results = [], []
        self.hash_rows={}

    def data_digest(self):
        queries=[]
        for table, ddl in self.contract['tables'].items():
            columns=re.findall(r'^  `([^`]+)` ',ddl,re.M)
            fields=','.join('IF(`'+c+'` IS NULL,NULL,HEX(CAST(`'+c+'` AS BINARY)))' for c in columns)
            queries.append('SELECT '+literal(table)+',JSON_ARRAY('+fields+') FROM `'+table+'`')
        return sha('\n'.join(sorted(self.db.execute(' UNION ALL '.join(queries)).splitlines())))

    def add(self, table, row):
        self.db.execute(insert(table, row))

    def reject(self, sql, tables, codes=(1644,), mode=None):
        before = self.data_digest()
        self.db.scalar('SELECT 1') # identity guard before the independent DML client
        header=('SET SESSION sql_mode='+literal(mode)+';\n') if mode is not None else ''
        result = subprocess.run(self.db.args, input=header+'USE `' + self.config['database'] + '`;\nSTART TRANSACTION;\n' + sql + ';\nROLLBACK;\n', text=True, capture_output=True, timeout=120)
        match = re.search(r'ERROR (\d+) \(', result.stderr)
        unchanged=self.data_digest()==before
        passed=result.returncode!=0 and match is not None and int(match[1]) in (*codes,3819) and unchanged
        record=dict(statementHash=sha(sql),status='PASS' if passed else 'FAIL',mysqlErrno=int(match[1]) if match else None,dataUnchanged=unchanged,mode=mode,context=getattr(self,'context',None))
        self.results.append(record)
        if not passed:self.failures.append(record)
        require(unchanged,'Rejected DML changed persistent data; stop to preserve evidence')

    def api(self, sql):
        env = dict(os.environ, MYSQL_PWD='Synthetic-ci-only-credential')
        return subprocess.run(self.db.tcp_args + ['--user=' + self.config['apiUser']] + (['--password=Synthetic-ci-only-credential'] if self.config.get('isolatedLocal') else []),
                              input='USE `' + self.config['database'] + '`;\n' + sql + ';\n', text=True, capture_output=True, env=env, timeout=30)

    def reproduce_predecessor_failure(self):
        _, table, row, _ = self.rows()[0]
        before = fingerprint(self.db, {'tables':{table:None}})
        result = subprocess.run(self.db.args, input='USE `' + self.config['database'] + '`;\n' + insert(table,row) + ';\n', text=True, capture_output=True, timeout=120)
        require(result.returncode != 0 and 'ERROR 3995 ' in result.stderr, 'Predecessor 3995 was not reproduced through invitation INSERT')
        require(fingerprint(self.db, {'tables':{table:None}}) == before, 'Failed predecessor INSERT persisted data')

    def seed(self):
        i = self.ids
        for key in ('user', 'targetUser'):
            self.add('user', dict(id=i[key], email=i[key] + '@example.test', password_hash='synthetic-not-a-real-password', role='admin'))
        for key in ('tenant', 'other'):
            self.add('institution', dict(id=i[key], code='MT5BP_' + uuid.UUID(i[key]).hex, name='Synthetic runtime', status='active'))
        for key, user in [('member', 'user'), ('target', 'targetUser')]:
            self.add('institution_membership', dict(id=i[key], institution_id=i['tenant'], user_id=i[user], status='active'))
        self.add('institution_membership_role', dict(membership_id=i['member'], role='InstitutionAdmin'))
        self.add('institution_membership_role', dict(membership_id=i['target'], role='Student'))
        self.add('study_path', dict(id=i['path'], institution_id=i['tenant'], name='Synthetic path'))
        self.add('formative_plan', dict(id=i['plan'], institution_id=i['tenant'], study_path_id=i['path'], code='SYNTHETIC', name='Synthetic plan', revision=0, created_at_utc=self.created))

    def rows(self):
        i, h = self.ids, self.hash
        invite = dict(institution_id=i['tenant'], id=i['invite'], recipient_user_id=i['targetUser'], kind='Membership', roles=['Student'], status='pending', token_hash=h, channel_hash=h, expires_at_utc=self.expiry, attempts=0, created_at_utc=self.created)
        event = dict(institution_id=i['tenant'], id=i['event'], actor_kind='Operator', operator_id='synthetic-operator', key_id='synthetic-key', action='Provision', intent_hash=h, reason='Synthetic verification', result_json={}, occurred_at_utc=self.created)
        nonce = dict(operator_id='synthetic-operator', nonce=h, key_id='synthetic-key', institution_id=i['tenant'], event_id=i['event'], expires_at_utc=self.expiry)
        batch = dict(id=i['batch'], institution_id=i['tenant'], actor_user_id=i['user'], actor_membership_id=i['member'], category='Student', state='Preview', revision=1, file_sha256=h, preview_digest=h, total_rows=1, valid_rows=1, invalid_rows=0, payload={}, warnings_json=[], created_at_utc=self.created, expires_at_utc=self.expiry)
        import_event = dict(institution_id=i['tenant'], id=str(uuid.uuid4()), batch_id=i['batch'], revision=1, action='Previewed', actor_kind='User', actor_user_id=i['user'], actor_membership_id=i['member'], request_hash=h, result_json={}, reason='Synthetic import', occurred_at_utc=self.created)
        formative = dict(id=str(uuid.uuid4()), institution_id=i['tenant'], plan_id=i['plan'], actor_user_id=i['user'], actor_membership_id=i['member'], operation='Created', revision_before=0, revision_after=0, request_hash=h, after_json={}, reason='Synthetic plan', occurred_at_utc=self.created)
        return [('mt5c_invitation_insert', 'institution_invitation', invite, ['token_hash', 'channel_hash']),
                ('mt5c_event_insert', 'institution_onboarding_event', event, ['intent_hash']),
                ('mt5c_nonce_insert', 'platform_operation_nonce', nonce, ['nonce']),
                ('import_batch_insert', 'institution_import_batch', batch, ['file_sha256', 'preview_digest']),
                ('import_event_insert', 'institution_import_event', import_event, ['request_hash']),
                ('fpe_insert', 'formative_plan_event', formative, ['request_hash'])]

    def matrix(self, name, table, row, fields):
        for field in fields:
          for mode in [None,'','NO_BACKSLASH_ESCAPES']:
            for bad in [self.hash[:-1], self.hash + 'A', 'G' + self.hash[1:], self.hash.lower(), 'é' + self.hash[1:], self.hash + '\n', self.hash+' ', ' '+self.hash, self.hash+'\r\n', self.hash+'\t', self.hash+' '*1024, None]:
                mutated = dict(row, **{field: bad})
                # NOT NULL/ASCII/width may reject before the trigger. The direct
                # ICU matrix separately covers those values as character input.
                try:
                    self.context=dict(trigger=name,table=table,column=field,input=repr(bad),operation='INSERT')
                    self.reject(insert(table, mutated), [table], (1644, 1048, 1366, 1406),mode=mode)
                except Exception as error:
                    raise RuntimeError(f'{name}: {field}={bad!r}: {error}') from error
        self.add(table, row)
        self.hash_rows[table]=(dict(row),fields)
        self.coverage.append(name)

    def core(self):
        i = self.ids
        visible = self.api('SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE()')
        require(visible.returncode == 0 and visible.stdout.strip() == '0', 'API trigger metadata privilege expanded')
        denied = self.api('CREATE TABLE er_api_forbidden(id int)')
        require(denied.returncode != 0 and 'ERROR 1142 ' in denied.stderr, 'API unexpectedly has DDL privilege')
        for spec in self.rows():
            self.matrix(*spec)
        invitation = 'institution_invitation'
        where = " WHERE institution_id=" + literal(i['tenant']) + ' AND id=' + literal(i['invite'])
        for field in ('token_hash', 'channel_hash'):
            for bad in ['a' * 64, 'G' + 'A' * 63, 'A' * 63, 'A' * 65, 'é' + 'A' * 63, None]:
                self.reject('UPDATE ' + invitation + ' SET ' + field + '=' + value(bad) + where, [invitation], (1644, 1048, 1366, 1406))
        self.db.execute('UPDATE ' + invitation + " SET attempts=1" + where)
        self.reject('UPDATE ' + invitation + ' SET recipient_user_id=' + literal(i['user']) + where, [invitation])
        self.db.execute('UPDATE ' + invitation + " SET status='consumed',consumed_membership_id=" + literal(i['target']) + where)
        self.coverage.append('mt5c_invitation_update')
        # Membership status changes must increment security generations. The
        # real audit row must describe the already-persisted mutation exactly.
        for status, before, action in [('suspended', 'active', 'Suspend'), ('active', 'suspended', 'Reactivate')]:
            old = int(self.db.scalar('SELECT security_generation FROM institution_membership_security_state WHERE membership_id=' + literal(i['target'])))
            self.db.execute('UPDATE institution_membership SET status=' + literal(status) + ',suspended_at=' + value(self.created if status == 'suspended' else None) + ' WHERE id=' + literal(i['target']))
            require(int(self.db.scalar('SELECT security_generation FROM institution_membership_security_state WHERE membership_id=' + literal(i['target']))) == old + 1, 'Membership security generation failed')
            row = dict(institution_id=i['tenant'], id=str(uuid.uuid4()), actor_user_id=i['user'], actor_membership_id=i['member'], target_membership_id=i['target'], action=action,
                       previous_status=before, new_status=status, previous_roles=['Student'], new_roles=['Student'], reason='Synthetic status', revision_before=old, revision_after=old + 1, request_hash=self.hash, occurred_at_utc=self.created)
            for bad in ['a'*64, 'A'*63, 'G'+'A'*63, 'A'*65, 'é'+'A'*63, None]:
                self.reject(insert('institution_membership_event', dict(row, request_hash=bad)), ['institution_membership_event'], (1644, 1048, 1366, 1406))
            self.reject(insert('institution_membership_event', dict(row, actor_membership_id=i['target'], actor_user_id=i['targetUser'], target_membership_id=i['member'])), ['institution_membership_event'])
            self.reject(insert('institution_membership_event', dict(row, institution_id=i['other'])), ['institution_membership_event'])
            self.matrix('mt5d_event_insert','institution_membership_event',row,['request_hash'])
        self.coverage.append('mt5d_event_insert')
        # Role removal is the application's membership permission revocation.
        generation = int(self.db.scalar('SELECT security_generation FROM institution_membership_security_state WHERE membership_id=' + literal(i['target'])))
        self.db.execute("DELETE FROM institution_membership_role WHERE membership_id=" + literal(i['target']) + " AND role='Student'")
        require(int(self.db.scalar('SELECT security_generation FROM institution_membership_security_state WHERE membership_id=' + literal(i['target']))) == generation + 1, 'Role revocation failed')
        role_event = dict(institution_id=i['tenant'], id=str(uuid.uuid4()), actor_user_id=i['user'], actor_membership_id=i['member'], target_membership_id=i['target'], action='RemoveRole', target_role='Student',
                         previous_status='active', new_status='active', previous_roles=['Student'], new_roles=[], reason='Synthetic role revocation', revision_before=generation, revision_after=generation+1, request_hash=self.hash, occurred_at_utc=self.created)
        self.add('institution_membership_event',role_event)
        self.db.execute("INSERT INTO institution_membership_role(membership_id,role) VALUES(" + literal(i['target']) + ",'Student')")
        self.add('institution_membership_event',dict(role_event,id=str(uuid.uuid4()),action='AddRole',previous_roles=[],new_roles=['Student'],revision_before=generation+1,revision_after=generation+2))
    def all(self):
        phases=[]
        for name,action in [('core',self.core),('email',self.email),('imports',self.import_people),('governance',self.governance),('calendar',self.calendar_and_version),('profile-hash',self.profile_hash),('calendar-import-hash',self.calendar_import_hash),('school-hashes',self.school_hashes),('hash-updates',self.hash_updates),('pending-hash-updates',self.pending_hash_updates),('retention',self.retention),('audit',self.audit_checks)]:
            try:
                before=len(self.failures);action();phases.append(dict(phase=name,status='PASS' if len(self.failures)==before else 'FAIL'))
            except Exception as error:
                phases.append(dict(phase=name,status='FAIL',error=str(error)));self.failures.append(phases[-1])
                if 'identity' in str(error).lower() or self.db.broken:
                    if self.config.get('acceptanceEvidenceFile'):
                        Path(self.config['acceptanceEvidenceFile']).write_text(json.dumps(dict(phases=phases,cases=self.results,failures=self.failures),indent=2))
                    raise
        evidence=dict(phases=phases,cases=self.results,failures=self.failures)
        if self.config.get('acceptanceEvidenceFile'):
            Path(self.config['acceptanceEvidenceFile']).write_text(json.dumps(evidence,indent=2))
        require(not self.failures,'Acceptance failures: '+json.dumps(self.failures))
        return dict(triggerDml=self.coverage,deleteGuards='Production 8.4 rejects fixture cleanup',ids=self.ids,matrix=evidence)

    def audit_checks(self):
        i=self.ids
        for table in ('institution_membership_event','institution_onboarding_event','institution_email_event','institution_import_event','formative_plan_event'):
            self.reject('UPDATE `' + table + "` SET reason=CONCAT(reason,'!') WHERE institution_id=" + literal(i['tenant']), [table])
        self.reject('UPDATE institution_membership SET institution_id=' + literal(i['other']) + ' WHERE id=' + literal(i['target']), ['institution_membership','institution_membership_security_state'])
        unauthorized = dict(self.rows()[5][2], actor_user_id=i['targetUser'], actor_membership_id=i['target'])
        self.reject(insert('formative_plan_event', unauthorized), ['formative_plan_event'])

    def profile_hash(self):
        i=self.ids
        row=dict(institution_id=i['tenant'],id=str(uuid.uuid4()),actor_user_id=i['user'],actor_membership_id=i['member'],target_user_id=i['targetUser'],target_membership_id=i['target'],teacher_id=i['teacher'],status='requires-review',request_hash=self.hash,reason='Synthetic profile validation')
        self.matrix('profile_acquisition_insert','institution_profile_acquisition',row,['request_hash'])

    def calendar_import_hash(self):
        i=self.ids; import_id=str(uuid.uuid4()); event_id=str(uuid.uuid4())
        event=dict(id=event_id,institution_id=i['tenant'],actor_user_id=i['user'],actor_membership_id=i['member'],resource_kind='Import',resource_id=import_id,operation='ImportPreviewed',revision_before=None,revision_after=0,request_hash=self.hash,state_json={'Revision':0},receipt_json={},reason='Synthetic calendar import',occurred_at_utc=self.created)
        self.add('institution_calendar_event',event)
        row=dict(id=import_id,institution_id=i['tenant'],actor_user_id=i['user'],actor_membership_id=i['member'],revision=0,status='Preview',file_sha256=self.hash,preview_digest=self.hash,raw_json=[],settings_json={},preview_json={},created_at_utc=self.created,expires_at_utc=self.expiry,event_id=event_id)
        self.matrix('institution_calendar_import_bi','institution_calendar_import',row,['file_sha256','preview_digest'])

    def school_hashes(self):
        i=self.ids; ids={k:str(uuid.uuid4()) for k in ['edition','student','enrollment','biennium','area','bsa','bsp','from','to']}
        self.add('course_edition',dict(id=ids['edition'],institution_id=i['tenant'],study_path_id=i['path'],code='CERT_'+uuid.uuid4().hex,name='Synthetic course',status='active',start_date='2026-01-01',end_date='2027-12-31'))
        self.add('student',dict(id=ids['student'],institution_id=i['tenant'],first_name='Synthetic',last_name='Transfer'))
        self.add('student_enrollment',dict(id=ids['enrollment'],institution_id=i['tenant'],student_id=ids['student'],course_edition_id=ids['edition'],enrollment_date='2026-01-01',status='enrolled',revision=0))
        day=self.created[:10]
        self.db.execute("UPDATE student_enrollment SET status='completed',completion_date="+literal(day)+",revision=1 WHERE id="+literal(ids['enrollment']))
        event=dict(institution_id=i['tenant'],id=str(uuid.uuid4()),actor_user_id=i['user'],actor_membership_id=i['member'],course_edition_id=ids['edition'],enrollment_id=ids['enrollment'],action='enrollment.transition',previous_status='enrolled',new_status='completed',new_completion_date=day,revision_before=0,revision_after=1,reason='Synthetic lifecycle',request_hash=self.hash,after_json='{}',occurred_at_utc=self.created)
        self.matrix('mt4c_course_event_insert','course_administration_event',event,['request_hash'])
        # A separate enrollment supplies a legitimate same-course transfer.
        student=str(uuid.uuid4()); enrollment=str(uuid.uuid4())
        self.add('biennium',dict(id=ids['biennium'],institution_id=i['tenant'],start_year='2026-01-01',end_year='2027-12-31'))
        self.add('study_area',dict(id=ids['area'],institution_id=i['tenant'],name='Synthetic area',description='Synthetic certification fixture'))
        self.add('biennium_study_area',dict(id=ids['bsa'],institution_id=i['tenant'],biennium_id=ids['biennium'],study_area_id=ids['area']))
        self.add('biennium_study_path',dict(id=ids['bsp'],institution_id=i['tenant'],biennium_study_area_id=ids['bsa'],study_path_id=i['path']))
        for key in ['from','to']:self.add('class',dict(id=ids[key],institution_id=i['tenant'],biennium_study_path_id=ids['bsp'],name=key,course_edition_id=ids['edition']))
        self.add('student',dict(id=student,institution_id=i['tenant'],first_name='Synthetic',last_name='Transfer',class_id=ids['from']))
        self.add('student_enrollment',dict(id=enrollment,institution_id=i['tenant'],student_id=student,course_edition_id=ids['edition'],enrollment_date='2026-01-01',status='enrolled',revision=0))
        self.db.execute('UPDATE student SET class_id='+literal(ids['to'])+',transfer_revision=1 WHERE id='+literal(student))
        row=dict(institution_id=i['tenant'],id=str(uuid.uuid4()),student_id=student,from_class_id=ids['from'],to_class_id=ids['to'],from_course_edition_id=ids['edition'],to_course_edition_id=ids['edition'],source_enrollment_id=enrollment,destination_enrollment_id=enrollment,actor_user_id=i['user'],actor_membership_id=i['member'],effective_date=day,source_course_start_date='2026-01-01',destination_course_start_date='2026-01-01',occurred_at_utc=self.created,applied_limit_months=24,revision_before=0,revision_after=1,reason='Synthetic transfer',request_hash=self.hash,from_class_name='from',to_class_name='to',from_course_name='Synthetic',to_course_name='Synthetic')
        self.matrix('transfer_event_insert','student_class_transfer_event',row,['request_hash'])

    def hash_updates(self):
        for table,fields in self.contract['hashColumns'].items():
            require(table in self.hash_rows,'Missing real INSERT control for '+table)
            row,_=self.hash_rows[table]
            keys=re.search(r'PRIMARY KEY \(([^)]+)\)',self.contract['tables'][table])[1].replace('`','').split(',')
            where=' AND '.join('`'+key+'`='+value(row[key]) for key in keys)
            for field in fields:
                for mode in [None,'','NO_BACKSLASH_ESCAPES']:
                    for bad in [self.hash+'\n',self.hash+' ',self.hash+'\r\n',self.hash+'\t',' '+self.hash,self.hash.lower(),'G'+self.hash[1:],'é'+self.hash[1:],self.hash[:-1],self.hash+'A',self.hash+' '*1024]:
                        self.context=dict(table=table,column=field['name'],input=repr(bad),operation='UPDATE')
                        self.reject('UPDATE `'+table+'` SET `'+field['name']+'`='+value(bad)+' WHERE '+where,[table],(1644,1048,1366,1406),mode=mode)

    def pending_hash_updates(self):
        # A pending invitation permits hash rotation, so its negative UPDATEs
        # cannot pass merely because an unrelated consumed/immutable guard fires.
        row=dict(self.rows()[0][2],id=str(uuid.uuid4()))
        self.add('institution_invitation',row)
        where=' WHERE institution_id='+literal(self.ids['tenant'])+' AND id='+literal(row['id'])
        for field in ('token_hash','channel_hash'):
            for mode in (None,'','NO_BACKSLASH_ESCAPES'):
                for bad in ['A'*64+'\n','\n'+'A'*63,'A'*31+'\n'+'A'*32,'A'*31+' '+'A'*32,'A'*63+'\u00a0','A'*63+'\u200b','\uff21'*64,'A'*63+'\U0001f600','A'*63+'\0']:
                    self.context=dict(table='institution_invitation',column=field,input=repr(bad),operation='UPDATE pending invitation')
                    self.reject('UPDATE institution_invitation SET `'+field+'`='+value(bad)+where,['institution_invitation'],(1644,1048,1366,1406),mode=mode)
            valid='0123456789ABCDEF'*4
            self.db.execute('UPDATE institution_invitation SET `'+field+'`='+literal(valid)+where)
            require(self.db.scalar('SELECT BINARY `'+field+'`='+literal(valid)+' FROM institution_invitation'+where)=='1','Valid hash update did not persist')
        self.coverage.append('mt5c_invitation_update:pending-hash')
        return dict(pendingInvitation=row['id'],validUpdates=2)

    def email(self):
        i = self.ids
        # Minimal policy satisfying the actual policy trigger, followed by its audit.
        policy = dict(institution_id=i['tenant'], policy_id=str(uuid.uuid4()), category='Student', revision=1, state='Draft', configuration={},
                      actor_user_id=i['user'], actor_membership_id=i['member'], created_at_utc=self.created)
        self.add('institution_email_policy', policy)
        event = dict(institution_id=i['tenant'], id=str(uuid.uuid4()), actor_user_id=i['user'], actor_membership_id=i['member'], operation='PolicyWritten', category='Student', policy_revision=1, request_hash=self.hash, after_json={}, reason='Synthetic email', occurred_at_utc=self.created)
        self.matrix('email_event_insert', 'institution_email_event', event, ['request_hash'])

    def governance(self):
        i = self.ids
        # A separate suspended owner and Recovery invitation are required to
        # reach governance retention with real related records.
        self.db.execute("UPDATE institution SET status='suspended' WHERE id=" + literal(i['other']))
        invite = dict(institution_id=i['other'], id=i['recovery'], recipient_user_id=i['user'], kind='Recovery', roles=['InstitutionAdmin'], status='pending', token_hash=self.hash, channel_hash=self.hash, expires_at_utc=self.expiry, attempts=0, created_at_utc=self.created)
        self.add('institution_invitation', invite)
        for key, action in [('recoveryEvent', 'RecoveryRequested'), ('approvalEvent', 'RecoveryApproved')]:
            self.add('institution_onboarding_event', dict(institution_id=i['other'], id=i[key], actor_kind='Operator', operator_id='synthetic-operator', key_id='key', action=action, intent_hash=self.hash, reason='Synthetic recovery', result_json={}, occurred_at_utc=self.created))
        self.add('institution_recovery', dict(institution_id=i['other'], id=i['recovery'], recipient_user_id=i['user'], request_event_id=i['recoveryEvent'], signed_request={'OperatorId':'synthetic-operator'}, expires_at_utc=self.expiry))
        self.add('institution_recovery_approval', dict(institution_id=i['other'], recovery_id=i['recovery'], operator_id='synthetic-operator', event_id=i['approvalEvent'], signed_operation={'OperatorId':'synthetic-operator'}))
        # Provisioning requires its distinct FirstAdmin invitation and attributed
        # Provision approval, so this is not an unreachable dummy delete.
        first = str(uuid.uuid4())
        self.add('institution_invitation', dict(invite, institution_id=i['tenant'], id=first, kind='FirstAdmin'))
        self.add('institution_provisioning', dict(institution_id=i['tenant'], approval_event_id=i['event'], first_invitation_id=first, legal_identity='Synthetic legal identity', evidence_reference='synthetic-evidence'))

    def import_people(self):
        i = self.ids
        for category in ('Student', 'Teacher'):
            person = str(uuid.uuid4())
            i[category.lower()] = person
            self.add(category.lower(), dict(id=person, institution_id=i['tenant'], first_name='Synthetic', last_name=category))
            batch = i['batch']
            if category == 'Teacher':
                row = dict(self.rows()[3][2], id=str(uuid.uuid4()), category=category)
                batch = row['id']
                self.matrix('import_batch_insert:Teacher', 'institution_import_batch', row, ['file_sha256','preview_digest'])
            self.add('institution_import_item', dict(institution_id=i['tenant'], batch_id=batch, row_number=1, category=category,
                                                     **{category.lower()+'_id':person}))
            self.db.execute("UPDATE institution_import_batch SET state='Completed',revision=2,payload=NULL,result_json=JSON_OBJECT() WHERE id=" + literal(batch))
            event = dict(self.rows()[4][2], id=str(uuid.uuid4()), batch_id=batch, revision=2, action='Confirmed')
            self.matrix('import_event_insert:Confirmed:'+category, 'institution_import_event', event, ['request_hash'])
        self.add('institution_membership_role', dict(membership_id=i['target'], role='Teacher'))
        self.add('institution_teacher_profile', dict(institution_id=i['tenant'], membership_id=i['target'], teacher_id=i['teacher']))
        self.reject(insert('institution_teacher_profile', dict(institution_id=i['other'], membership_id=i['target'], teacher_id=i['teacher'])), ['institution_teacher_profile'], (1452,1644))

    def calendar_and_version(self):
        i = self.ids
        version = str(uuid.uuid4())
        self.add('formative_plan_version', dict(id=version, institution_id=i['tenant'], study_path_id=i['path'], plan_id=i['plan'], version_number=1, name='Synthetic draft', status='Draft', revision=0, theory_planned_minutes=60, internship_planned_minutes=0, created_at_utc=self.created))
        # A Teacher acting through an unlinked membership must be rejected by the
        # real calendar authorization predicate, even with otherwise shaped JSON.
        event = dict(id=str(uuid.uuid4()), institution_id=i['tenant'], actor_user_id=i['user'], actor_membership_id=i['member'], resource_kind='Settings', resource_id=i['tenant'], operation='SettingsCreated', revision_before=None, revision_after=0, request_hash=self.hash, state_json={'Revision':0}, receipt_json={}, reason='Synthetic calendar', occurred_at_utc=self.created)
        self.matrix('institution_calendar_event_bi','institution_calendar_event',event,['request_hash'])
        self.add('institution_calendar_settings', dict(institution_id=i['tenant'], time_zone_id='Europe/Rome', approval_lead_days=1, revision=0, event_id=event['id']))
        self.reject(insert('institution_calendar_event', dict(event, id=str(uuid.uuid4()), institution_id=i['other'], revision_after=1)), ['institution_calendar_event'])
        self.reject(insert('institution_calendar_event', dict(event, id=str(uuid.uuid4()), resource_kind='Lesson', resource_id=str(uuid.uuid4()), operation='LessonCompleted',
                                                             actor_user_id=i['targetUser'], actor_membership_id=i['target'], state_json={'Status':'Completed'})), ['institution_calendar_event'])

    def retention(self):
        i = self.ids
        spec = [('mt5c_invitation_delete','institution_invitation',i['tenant']), ('mt5d_event_delete','institution_membership_event',i['tenant']),
                ('mt5c_event_delete','institution_onboarding_event',i['tenant']), ('mt5c_nonce_delete','platform_operation_nonce',i['tenant']),
                ('mt5c_provisioning_delete','institution_provisioning',i['tenant']), ('mt5gov_recovery_delete','institution_recovery',i['other']),
                ('mt5gov_approval_delete','institution_recovery_approval',i['other']), ('mt5b_security_delete','institution_membership_security_state',i['tenant']),
                ('mt5b_membership_delete_contract','institution_membership',i['tenant'])]
        for name, table, owner in spec:
            require(int(self.db.scalar('SELECT COUNT(*) FROM `' + table + '` WHERE institution_id=' + literal(owner))) > 0, 'Empty DELETE probe')
            self.reject('DELETE FROM `' + table + '` WHERE institution_id=' + literal(owner), [table], (1451, 1644))
            self.coverage.append(name)
        # The second membership DELETE trigger cannot run when its first guard
        # deliberately signals 1451. Verify ordering + its code regex directly;
        # never disable the first trigger to manufacture coverage.
        require(self.db.scalar("SELECT ACTION_ORDER FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME='mt5b_membership_security_delete'") == '2', 'DELETE order changed')


def run(config_path, output):
    config = json.loads(Path(config_path).read_text(encoding='utf-8-sig'))
    local = config.get('isolatedLocal') is True
    if local:
        from mysql84_local import attest
        attest(config)
    else:
        require(os.environ.get('GITHUB_ACTIONS') == 'true' and config.get('isolatedCI') is True, 'Disposable CI config required')
    container = config.get('mysqlContainer', '')
    require(re.fullmatch(r'[a-f0-9]{64}', container), 'Explicit disposable service container ID required')
    image = subprocess.check_output(['docker','inspect','--format','{{.Config.Image}}',container],text=True).strip()
    require(image == json.loads((ROOT/'infra/staging/mysql-image.json').read_text())['image'], 'Service image digest differs from staging')
    output = Path(output)
    output.mkdir(parents=True, exist_ok=False)
    config['acceptanceEvidenceFile']=str(output/'upgrade-acceptance.json')
    subprocess.run(['node', str(ROOT / 'tools/mysql84-release.cjs')], check=True)
    db = Mysql(config)
    diagnostic = RuntimeDiagnostic(output/'runtime-phases.json', ['identity-and-legacy-bootstrap', 'seed-and-predecessor', 'snapshot', 'drift-rejection', 'ddl-recovery', 'trigger-routine-acceptance', 'fresh-install-after-upgrade'])
    try:
        with diagnostic.phase('identity-and-legacy-bootstrap'):
            # The CI caller supplies a brand-new unpredictable name. Never DROP or
            # reuse an existing schema, even on a disposable service.
            require(config['database'].startswith('er84_'), 'Unsafe namespace')
            db.execute('CREATE DATABASE `' + config['database'] + '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci')
            identity(db, config)
            for role in ('api', 'review'):
                db.execute("CREATE USER '" + config[role+'User'] + "'@'" + config[role+'Host'] + "' IDENTIFIED BY 'Synthetic-ci-only-credential'" + (' REQUIRE SSL' if local else ''))
            binding_file = output / 'binding.json'
            binding_file.write_text(json.dumps(config))
            bundle = output / 'old-bundle'
            subprocess.run(['node', str(ROOT / 'tools/mysql84-release.cjs'), 'bundle', str(binding_file), str(bundle), 'old'], check=True)
            for file in sorted(bundle.glob('*.sql')):
                # Execute the immutable legacy-derived provisioning bundle.
                sql=file.read_text()
                first=re.search(r'(?m)^CREATE (?:DEFINER=\S+ )?TRIGGER ',sql)
                if file.name.endswith('001_schema.sql') and first:
                    # Restore legitimate historical data during initial database
                    # restore, before any trigger exists. No installed protection
                    # is disabled and the immutable bundle bytes stay unchanged.
                    db.execute_script(sql[:first.start()]+'\nDELIMITER ;\n')
                    owner,event=str(uuid.uuid4()),str(uuid.uuid4())
                    db.execute(insert('institution',dict(id=owner,code='MT5BP_'+uuid.UUID(owner).hex,name='Historical synthetic owner',status='active')))
                    db.execute(insert('institution_onboarding_event',dict(institution_id=owner,id=event,actor_kind='Operator',operator_id='historical-synthetic',key_id='historical-key',action='Provision',intent_hash='0123456789ABCDEF'*4,reason='Historical legitimate hash',result_json={},occurred_at_utc=datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S.%f'))))
                    db.execute(insert('platform_operation_nonce',dict(institution_id=owner,event_id=event,operator_id='historical-synthetic',key_id='historical-key',nonce='0123456789ABCDEF'*4,expires_at_utc=(datetime.datetime.now()+datetime.timedelta(hours=3)).strftime('%Y-%m-%d %H:%M:%S.%f'))))
                    (output/'historical-hash-seed.json').write_text(json.dumps(dict(owner=owner,event=event,hash='0123456789ABCDEF'*4)))
                    db.execute_script('DELIMITER $$\n'+sql[first.start():])
                else:db.execute_script(sql)
            old = load_contract(False)
            verify_schema(db, old, config)
        with diagnostic.phase('seed-and-predecessor'):
            probes = Probes(db, config)
            probes.seed()
            probes.reproduce_predecessor_failure()
        with diagnostic.phase('snapshot'):
            before = fingerprint(db, old)
            (output/'before.json').write_text(json.dumps(before, indent=2))
            dump_args = [config.get('dumpClient','mysqldump'), '--defaults-extra-file='+config['defaultsFile'], '--host='+config['host'], '--port='+str(config['port']), '--protocol=TCP', '--ssl-mode=' + ('REQUIRED' if local else 'DISABLED'), '--single-transaction', '--set-gtid-purged=OFF', config['database']]
            with (output/'synthetic-before.sql').open('w') as stream:
                subprocess.run(dump_args, stdout=stream, check=True)
        with diagnostic.phase('drift-rejection'):
            # Deliberate predecessor drift must be rejected BEFORE maintenance/DDL.
            db.execute('ALTER TABLE institution ADD COLUMN synthetic_drift int NULL')
            try:
                Runner(db, config, output/'drift-journal.json').upgrade()
                raise AssertionError('Drift accepted')
            except RuntimeError:
                require(not (output/'drift-journal.json').exists(), 'Drift changed journal')
            db.execute('ALTER TABLE institution DROP COLUMN synthetic_drift')
            verify_schema(db, old, config)
        with diagnostic.phase('ddl-recovery'):
            from mysql84_recovery import container_binding, resume_channel
            config['recoveryBinding'] = container_binding(config)
            require(config.get('recoverySocketAccount') == 'root@localhost', 'Explicit disposable recovery account required')
            journal = output/'upgrade-journal.json'
            first_table=next(iter(load_contract()['tableChanges']))
            for fault,recovery,restart in [(first_table+':after-alter',False,True),('email_event_insert:after-drop',True,False),('fpe_insert:after-create',True,False),('routine:after-drop',True,False),('routine:after-create',True,False)]:
                try:
                    Runner(db, config, journal).upgrade(recover=recovery, fault=fault)
                    raise AssertionError('Fault injection not reached')
                except RuntimeError as error:
                    require('Injected interruption' in str(error), 'Unexpected failure before injected DDL boundary: '+str(error))
                require(db.scalar('SELECT @@global.offline_mode') == '1', 'Maintenance lost after interruption')
                blocked = probes.api('SELECT 1')
                require(blocked.returncode != 0 and 'ERROR 3032 ' in blocked.stderr, 'API can connect during incomplete DDL')
                # Simulate loss of the migration connection and its GET_LOCK; the
                # persisted barrier and durable journal must survive independently.
                db.close()
                if restart:
                    # This is the service created by this workflow, never an external
                    # container. Restart preserves its data volume and UUID.
                    if local:
                        # Never hide a Docker restart inside approval for a Python
                        # runner. Root requests approval for the single Docker command.
                        (output/'restart-request.json').write_text(json.dumps(dict(container=container, uuid=config['serverUuid'])))
                        approval = output/'restart-completed.json'
                        wait_until = time.monotonic() + 900
                        while not approval.exists():
                            require(time.monotonic() < wait_until, 'Explicit local restart approval not completed')
                            time.sleep(1)
                        require(json.loads(approval.read_text()) == dict(container=container, uuid=config['serverUuid']), 'Restart attestation mismatch')
                    else:
                        subprocess.run(['docker','restart',container],check=True)
                    config = resume_channel(config, db.tcp_args, output/'restart-result.json')
                    published_config = json.loads(Path(config_path).read_text(encoding='utf-8-sig'))
                    published_config['port'] = config['port']
                    Path(config_path).write_text(json.dumps(published_config))
                    db = Mysql(config)
                    probes.db, probes.config = db, config
                    blocked = probes.api('SELECT 1')
                    require(blocked.returncode != 0 and 'ERROR 3032 (HY000)' in blocked.stderr, 'Persisted maintenance barrier lost on server restart')
                    db.close()
                db = Mysql(config)
                identity(db, config)
                probes.db = db
                saved = journal.read_bytes()
                try:
                    Runner(db, config, journal).upgrade()
                    raise AssertionError('Blind replay accepted')
                except RuntimeError as error:
                    require('explicit recover required; no DDL replay' in str(error), 'Wrong blind replay rejection: '+str(error))
                    require(journal.read_bytes() == saved, 'Refused replay changed journal')
                if restart:
                    # A real persistent drift must block recovery before any
                    # uncertain ALTER is accepted or further release DDL runs.
                    db.execute('ALTER TABLE institution ADD COLUMN recovery_drift INT NULL')
                    try:
                        Runner(db, config, journal).upgrade(recover=True)
                        raise AssertionError('Recovery accepted persistent schema drift')
                    except RuntimeError as error:
                        require('Table DDL drift: institution;' in str(error), 'Wrong recovery drift rejection: '+str(error))
                        require(journal.read_bytes() == saved, 'Rejected recovery advanced persistent journal')
                        require(db.scalar('SELECT @@global.offline_mode') == '1', 'Rejected recovery removed maintenance')
                    db.execute('ALTER TABLE institution DROP COLUMN recovery_drift')
                    (output/'recovery-result.json').write_text(json.dumps(dict(blindReplayRejected=True,
                        persistentSchemaDriftRejected=True, refusedRecoveryJournalUnchanged=True, barrierRetained=True)))
            Runner(db, config, journal).upgrade(recover=True)
            # The authorized runner has now removed the barrier. Return to the
            # original transport; recovery-only guards require maintenance ON.
            db.close()
            config.pop('recoverySocket', None)
            db = Mysql(config)
            identity(db, config)
            probes.db = db
            require(db.scalar('SELECT @@global.offline_mode') == '0', 'Completion retained maintenance barrier')
            require(db.scalar("SELECT VARIABLE_VALUE FROM performance_schema.persisted_variables WHERE VARIABLE_NAME='offline_mode'") == 'OFF', 'Completion did not persist barrier removal')
            require(fingerprint(db, load_contract()) == before, 'Existing data lost')
            saved = journal.read_bytes()
            Runner(db, config, journal).upgrade()
            require(saved == journal.read_bytes(), 'Completed upgrade replayed')
            guard='ck_er84_institution_invitation_token_hash'
            db.execute('ALTER TABLE institution_invitation ALTER CHECK `'+guard+'` NOT ENFORCED')
            try:
                Runner(db,config,journal).upgrade()
                raise AssertionError('Disabled CHECK drift accepted')
            except RuntimeError:
                require(saved==journal.read_bytes(),'Drift rejection changed completed journal')
                body=load_contract()['routine']['body']
                require(db.scalar(body[len('BEGIN\n'):-len('\nEND')].strip())=='0','Readiness accepted a disabled hash constraint')
            finally:db.execute('ALTER TABLE institution_invitation ALTER CHECK `'+guard+'` ENFORCED')
            verify_schema(db,load_contract(),config)
        with diagnostic.phase('trigger-routine-acceptance'):
            acceptance = probes.all()
            verify_schema(db, load_contract(), config)
            (output/'runtime-result.json').write_text(json.dumps(dict(upgrade=True, predecessor3995Reproduced=True, snapshot=True, dataPreserved=True, idempotent=True, interruptedDropRecovered=True, lostCreateAckRecovered=True, persistedMaintenanceAcrossServerRestart=True, driftRejected=True, acceptance=acceptance), indent=2))
        with diagnostic.phase('fresh-install-after-upgrade'):
            fresh_install(db, config, output)
    finally:
        db.close()


def fresh_install(db, config, output):
    # A second new database exercises the actual direct-install command.
    fresh = copy.deepcopy(config)
    # RuntimePolicy confines the real HTTP application to this database in
    # LocalTest mode. CREATE without IF NOT EXISTS refuses any existing one.
    fresh['database'] = 'electronic_register_test'
    fresh['acceptanceEvidenceFile']=str(output/'fresh-acceptance.json')
    for role in ('api','review','definer'):
        fresh[role+'User'] += '_f'
    db.execute('CREATE DATABASE `' + fresh['database'] + '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci')
    for role in ('api','review'):
        db.execute("CREATE USER '" + fresh[role+'User'] + "'@'" + fresh[role+'Host'] + "' IDENTIFIED BY 'Synthetic-ci-only-credential'" + (' REQUIRE SSL' if config.get('isolatedLocal') else ''))
    fresh_config = output/'fresh-config.json'
    fresh_config.write_text(json.dumps(fresh))
    fresh_bundle = output/'fresh-bundle'
    subprocess.run(['node', str(ROOT/'tools/mysql84-release.cjs'),'bundle',str(fresh_config),str(fresh_bundle)],check=True)
    artifact = fresh_bundle/'98-new-release.sql'
    original_sql = artifact.read_text()
    original_binding = (fresh_bundle/'binding.json').read_text()
    altered_binding = json.loads(original_binding)
    artifact.write_text(original_sql+'\nSELECT 1;\n')
    altered_binding['files'][artifact.name] = sha(artifact.read_text())
    (fresh_bundle/'binding.json').write_text(json.dumps(altered_binding))
    try:
        install(db, fresh, output/'tampered-journal.json', fresh_bundle)
        raise AssertionError('Self-authorized tampered bundle accepted')
    except RuntimeError as error:
        require('Untrusted derived bundle' in str(error), 'Wrong tamper rejection')
        require(not (output/'tampered-journal.json').exists(), 'Tampered bundle wrote a journal')
    artifact.write_text(original_sql)
    (fresh_bundle/'binding.json').write_text(original_binding)
    install(db, fresh, output/'fresh-journal.json', fresh_bundle)
    install(db, fresh, output/'fresh-journal.json', fresh_bundle)
    fresh_probes = Probes(db, fresh)
    fresh_probes.seed()
    fresh_acceptance = fresh_probes.all()
    verify_schema(db, load_contract(), fresh)
    (output/'fresh-result.json').write_text(json.dumps(dict(fresh=True, idempotent=True, equivalentSchemaContract=True, acceptance=fresh_acceptance)))


def run_fresh(config_path, output):
    require(os.environ.get('GITHUB_ACTIONS') == 'true', 'Only disposable GitHub service accepted')
    config = json.loads(Path(config_path).read_text())
    require(config.get('isolatedCI') is True, 'CI config required')
    container = config.get('mysqlContainer', '')
    require(re.fullmatch(r'[a-f0-9]{64}', container), 'Explicit disposable service container ID required')
    image = subprocess.check_output(['docker','inspect','--format','{{.Config.Image}}',container],text=True).strip()
    require(image == json.loads((ROOT/'infra/staging/mysql-image.json').read_text())['image'], 'Service image digest differs from staging')
    output = Path(output)
    output.mkdir(parents=True, exist_ok=False)
    subprocess.run(['node', str(ROOT / 'tools/mysql84-release.cjs')], check=True)
    db = Mysql(config)
    diagnostic = RuntimeDiagnostic(output/'runtime-phases.json', ['direct-install-and-acceptance'])
    try:
        with diagnostic.phase('direct-install-and-acceptance'):
            fresh_install(db, config, output)
    finally:
        db.close()


if __name__ == '__main__':
    (run_fresh if '--fresh-only' in sys.argv else run)(sys.argv[1], sys.argv[2])
